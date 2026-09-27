using System.DirectoryServices;
using System.Security.Principal;

namespace NTFSSuite;

public sealed class SidOriginResolver : IDisposable
{
    private readonly DirectoryEntry rootDse;
    private readonly DirectoryEntry searchRoot;
    private readonly string currentDomainSid;
    private readonly string currentDomainName;
    private readonly Dictionary<string, SidOriginRecord> cache =
        new(StringComparer.OrdinalIgnoreCase);

    public SidOriginResolver()
    {
        rootDse = new DirectoryEntry("LDAP://RootDSE");
        string namingContext = Convert.ToString(
            rootDse.Properties["defaultNamingContext"].Value) ??
            throw new InvalidOperationException(
                "No fue posible obtener defaultNamingContext de Active Directory.");

        searchRoot = new DirectoryEntry("LDAP://" + namingContext);
        currentDomainName = Environment.UserDomainName;
        currentDomainSid = ReadCurrentDomainSid(searchRoot);
    }

    public SidOriginRecord Resolve(SecurityIdentifier sid)
    {
        if (cache.TryGetValue(sid.Value, out SidOriginRecord? cached))
            return CloneWithoutOccurrences(cached);

        var record = new SidOriginRecord
        {
            Sid = sid.Value,
            SidDomainPrefix = DomainPrefix(sid),
            CurrentDomainSid = currentDomainSid
        };

        string translatedAccount = TryTranslate(sid);
        bool translated = !string.IsNullOrWhiteSpace(translatedAccount) &&
                          !LooksLikeSid(translatedAccount);

        SearchResult? currentObject = FindByObjectSid(sid);
        if (currentObject is not null)
        {
            FillFromAd(record, currentObject);
            record.Origin = "ACTIVE DIRECTORY ACTUAL";
            record.MatchedBy = "objectSid";
            record.Status = "VIGENTE";
            record.Recommendation = "CONSERVAR. La ACL referencia una identidad vigente de Active Directory.";
            record.Risk = "BAJO";
            record.Evidence = "Coincidencia exacta con objectSid.";
            cache[sid.Value] = record;
            return CloneWithoutOccurrences(record);
        }

        SearchResult? historyObject = FindBySidHistory(sid);
        if (historyObject is not null)
        {
            FillFromAd(record, historyObject);
            record.Origin = "SID HISTORY";
            record.MatchedBy = "sIDHistory";
            record.Status = "IDENTIDAD MIGRADA O RENOMBRADA";
            record.Recommendation =
                "VALIDAR MIGRACIÓN Y DEPENDENCIAS. No eliminar la ACE hasta confirmar que la cuenta actual conserva el acceso requerido y que el SID histórico ya no es necesario.";
            record.Risk = "MEDIO";
            record.Evidence =
                "El SID fue localizado en el atributo sIDHistory de una identidad vigente.";
            cache[sid.Value] = record;
            return CloneWithoutOccurrences(record);
        }

        if (translated)
        {
            record.AccountName = translatedAccount;
            record.DisplayName = AccountLeaf(translatedAccount);
            record.PrincipalType = InferTranslatedType(translatedAccount, sid);

            if (IsBuiltInOrAuthority(translatedAccount))
            {
                record.Origin = "IDENTIDAD INTEGRADA DE WINDOWS";
                record.MatchedBy = "Traducción SID";
                record.Status = "VIGENTE";
                record.Recommendation =
                    "CONSERVAR, salvo que una revisión técnica determine que la identidad integrada no debe tener acceso a esa ruta.";
                record.Risk = "ALTO";
            }
            else if (IsLocalAccount(translatedAccount))
            {
                record.Origin = "CUENTA O GRUPO LOCAL";
                record.MatchedBy = "Traducción SID";
                record.Status = "RESUELTO LOCALMENTE";
                record.Recommendation =
                    "VALIDAR NECESIDAD OPERATIVA. Si la cuenta local ya no existe o no debe acceder, retirar la ACE mediante un cambio autorizado.";
                record.Risk = "MEDIO";
            }
            else
            {
                record.Origin = "DOMINIO EXTERNO O CONTEXTO NO CONSULTADO";
                record.MatchedBy = "Traducción SID";
                record.Status = "RESUELTO FUERA DEL DOMINIO ACTUAL";
                record.Recommendation =
                    "VALIDAR EL DOMINIO DE ORIGEN, la confianza y la vigencia de la cuenta antes de modificar la ACL.";
                record.Risk = "MEDIO";
            }

            record.Evidence = "Windows tradujo el SID a " + translatedAccount +
                              ", pero no se encontró objectSid ni sIDHistory en el dominio consultado.";
            cache[sid.Value] = record;
            return CloneWithoutOccurrences(record);
        }

        bool sameDomainPrefix =
            !string.IsNullOrWhiteSpace(currentDomainSid) &&
            record.SidDomainPrefix.Equals(
                currentDomainSid,
                StringComparison.OrdinalIgnoreCase);

        if (sameDomainPrefix)
        {
            record.Origin = "DOMINIO ACTUAL, OBJETO NO ENCONTRADO";
            record.MatchedBy = "Prefijo SID del dominio";
            record.Status = "POSIBLE SID HUÉRFANO";
            record.Recommendation =
                "CANDIDATO A ELIMINAR, únicamente después de confirmar con el cliente que la cuenta o grupo fue eliminado, revisar todas las rutas afectadas y contar con respaldo de las ACL.";
            record.Risk = "MEDIO";
            record.Evidence =
                "El prefijo coincide con el SID del dominio actual, pero no existe coincidencia por objectSid ni sIDHistory.";
        }
        else
        {
            record.Origin = "NO IDENTIFICADO";
            record.MatchedBy = "Sin coincidencia";
            record.Status = "SID NO RESUELTO";
            record.Recommendation =
                "REVISAR MANUALMENTE. Puede corresponder a otro dominio, una cuenta local de otro servidor, una identidad eliminada o una migración no visible desde el dominio actual. No eliminar automáticamente.";
            record.Risk = "ALTO";
            record.Evidence =
                "No fue posible traducir el SID ni localizarlo por objectSid o sIDHistory.";
        }

        record.AccountName = sid.Value;
        record.DisplayName = "SID NO RESUELTO";
        record.PrincipalType = "Desconocido";
        cache[sid.Value] = record;
        return CloneWithoutOccurrences(record);
    }

    private SearchResult? FindByObjectSid(SecurityIdentifier sid)
    {
        try
        {
            using var searcher = NewSearcher(
                "(&(objectClass=*)(objectSid=" + EscapeBinary(SidBytes(sid)) + "))");
            return searcher.FindOne();
        }
        catch
        {
            return null;
        }
    }

    private SearchResult? FindBySidHistory(SecurityIdentifier sid)
    {
        try
        {
            using var searcher = NewSearcher(
                "(&(objectClass=*)(sIDHistory=" + EscapeBinary(SidBytes(sid)) + "))");
            return searcher.FindOne();
        }
        catch
        {
            return null;
        }
    }

    private DirectorySearcher NewSearcher(string filter)
    {
        var searcher = new DirectorySearcher(searchRoot)
        {
            Filter = filter,
            SearchScope = SearchScope.Subtree,
            PageSize = 1000
        };
        foreach (string property in new[]
        {
            "displayName", "name", "sAMAccountName", "objectClass", "objectSid"
        })
            searcher.PropertiesToLoad.Add(property);
        return searcher;
    }

    private static void FillFromAd(SidOriginRecord record, SearchResult result)
    {
        record.DisplayName = Property(result, "displayName");
        if (string.IsNullOrWhiteSpace(record.DisplayName))
            record.DisplayName = Property(result, "name");

        string sam = Property(result, "sAMAccountName");
        record.AccountName = string.IsNullOrWhiteSpace(sam) ?
            Property(result, "name") : sam;

        record.PrincipalType = PrincipalType(result);

        if (result.Properties["objectSid"].Count > 0 &&
            result.Properties["objectSid"][0] is byte[] bytes)
            record.CurrentObjectSid = new SecurityIdentifier(bytes, 0).Value;
    }

    private static string PrincipalType(SearchResult result)
    {
        foreach (object value in result.Properties["objectClass"])
        {
            string objectClass = Convert.ToString(value) ?? "";
            if (objectClass.Equals("group", StringComparison.OrdinalIgnoreCase))
                return "Grupo";
            if (objectClass.Equals("computer", StringComparison.OrdinalIgnoreCase))
                return "Equipo";
            if (objectClass.Equals("user", StringComparison.OrdinalIgnoreCase))
                return "Usuario";
        }
        return "Objeto de Active Directory";
    }

    private static string Property(SearchResult result, string property) =>
        result.Properties[property].Count == 0 ? "" :
        Convert.ToString(result.Properties[property][0]) ?? "";

    private static string TryTranslate(SecurityIdentifier sid)
    {
        try
        {
            return ((NTAccount)sid.Translate(typeof(NTAccount))).Value;
        }
        catch
        {
            return "";
        }
    }

    private static string ReadCurrentDomainSid(DirectoryEntry domainRoot)
    {
        try
        {
            domainRoot.RefreshCache(new[] { "objectSid" });
            if (domainRoot.Properties["objectSid"].Value is byte[] bytes)
                return new SecurityIdentifier(bytes, 0).Value;
        }
        catch { }
        return "";
    }

    private static byte[] SidBytes(SecurityIdentifier sid)
    {
        byte[] result = new byte[sid.BinaryLength];
        sid.GetBinaryForm(result, 0);
        return result;
    }

    private static string EscapeBinary(byte[] bytes) =>
        string.Concat(bytes.Select(value => "\\" + value.ToString("X2")));

    private static string DomainPrefix(SecurityIdentifier sid)
    {
        string value = sid.Value;
        int index = value.LastIndexOf('-');
        return index > 0 ? value[..index] : value;
    }

    private static bool LooksLikeSid(string value) =>
        value.StartsWith("S-1-", StringComparison.OrdinalIgnoreCase);

    private static bool IsBuiltInOrAuthority(string account) =>
        account.StartsWith("BUILTIN\\", StringComparison.OrdinalIgnoreCase) ||
        account.StartsWith("NT AUTHORITY\\", StringComparison.OrdinalIgnoreCase) ||
        account.StartsWith("CREATOR OWNER", StringComparison.OrdinalIgnoreCase);

    private static bool IsLocalAccount(string account)
    {
        int separator = account.IndexOf('\\');
        if (separator <= 0) return false;
        string authority = account[..separator];
        return authority.Equals(
            Environment.MachineName,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string InferTranslatedType(
        string account,
        SecurityIdentifier sid)
    {
        if (IsBuiltInOrAuthority(account)) return "Identidad integrada";
        if (sid.IsWellKnown(WellKnownSidType.LocalSystemSid)) return "Identidad integrada";
        if (account.EndsWith("$", StringComparison.OrdinalIgnoreCase)) return "Equipo";
        return "Cuenta";
    }

    private static string AccountLeaf(string account)
    {
        int separator = account.LastIndexOf('\\');
        return separator >= 0 ? account[(separator + 1)..] : account;
    }

    private static SidOriginRecord CloneWithoutOccurrences(SidOriginRecord source) =>
        new()
        {
            Sid = source.Sid,
            SidDomainPrefix = source.SidDomainPrefix,
            AccountName = source.AccountName,
            DisplayName = source.DisplayName,
            PrincipalType = source.PrincipalType,
            Origin = source.Origin,
            MatchedBy = source.MatchedBy,
            CurrentObjectSid = source.CurrentObjectSid,
            CurrentDomainSid = source.CurrentDomainSid,
            Status = source.Status,
            Recommendation = source.Recommendation,
            Risk = source.Risk,
            Evidence = source.Evidence
        };

    public void Dispose()
    {
        searchRoot.Dispose();
        rootDse.Dispose();
    }
}
