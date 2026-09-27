using System.DirectoryServices;
using System.Security.Principal;

namespace NTFSSuite;

public sealed class SidOriginResolver : IDisposable
{
    private readonly DirectoryEntry rootDse;
    private readonly DirectoryEntry searchRoot;
    private readonly string currentDomainSid;
    private readonly Dictionary<string, SidOriginRecord> cache = new(StringComparer.OrdinalIgnoreCase);

    public SidOriginResolver()
    {
        rootDse = new DirectoryEntry("LDAP://RootDSE");
        string namingContext = Convert.ToString(rootDse.Properties["defaultNamingContext"].Value)
            ?? throw new InvalidOperationException("No fue posible obtener defaultNamingContext.");
        searchRoot = new DirectoryEntry("LDAP://" + namingContext);
        currentDomainSid = ReadCurrentDomainSid(searchRoot);
    }

    public SidOriginRecord Resolve(SecurityIdentifier sid)
    {
        if (cache.TryGetValue(sid.Value, out SidOriginRecord? cached))
            return Clone(cached);

        var record = new SidOriginRecord
        {
            Sid = sid.Value,
            Rid = Rid(sid),
            SidDomainPrefix = DomainPrefix(sid),
            CurrentDomainSid = currentDomainSid
        };

        SearchResult? current = FindByBinarySid("objectSid", sid);
        if (current is not null)
        {
            FillFromAd(record, current);
            record.Origin = "ACTIVE DIRECTORY ACTUAL";
            record.MatchedBy = "objectSid";
            record.Status = "VIGENTE";
            record.Recommendation = "CONSERVAR. Identidad vigente en Active Directory.";
            record.Risk = "BAJO";
            record.Evidence = "Coincidencia exacta con objectSid.";
            cache[sid.Value] = record;
            return Clone(record);
        }

        SearchResult? history = FindByBinarySid("sIDHistory", sid);
        if (history is not null)
        {
            FillFromAd(record, history);
            record.Origin = "SID HISTORY";
            record.MatchedBy = "sIDHistory";
            record.Status = "IDENTIDAD MIGRADA O RENOMBRADA";
            record.Recommendation = "VALIDAR MIGRACIÓN. No retirar la ACE sin confirmar que el acceso ya no depende del SID histórico.";
            record.Risk = "MEDIO";
            record.Evidence = "Coincidencia con sIDHistory de una identidad vigente.";
            cache[sid.Value] = record;
            return Clone(record);
        }

        string translated = TryTranslate(sid);
        if (!string.IsNullOrWhiteSpace(translated) && !translated.StartsWith("S-1-", StringComparison.OrdinalIgnoreCase))
        {
            record.AccountName = translated;
            record.DisplayName = Leaf(translated);
            record.PrincipalType = InferType(translated);
            record.MatchedBy = "Traducción Windows";
            record.Evidence = "Windows tradujo el SID, pero no se localizó en objectSid o sIDHistory del dominio consultado.";

            if (IsBuiltIn(translated))
            {
                record.Origin = "IDENTIDAD INTEGRADA DE WINDOWS";
                record.Status = "VIGENTE";
                record.Recommendation = "CONSERVAR, salvo revisión técnica específica.";
                record.Risk = "ALTO";
            }
            else if (IsLocal(translated))
            {
                record.Origin = "CUENTA O GRUPO LOCAL";
                record.Status = "RESUELTO LOCALMENTE";
                record.Recommendation = "VALIDAR necesidad operativa y existencia local antes de modificar la ACE.";
                record.Risk = "MEDIO";
            }
            else
            {
                record.Origin = "DOMINIO EXTERNO O CONTEXTO NO CONSULTADO";
                record.Status = "RESUELTO FUERA DEL DOMINIO ACTUAL";
                record.Recommendation = "VALIDAR dominio, confianza y vigencia antes de modificar la ACL.";
                record.Risk = "MEDIO";
            }
            cache[sid.Value] = record;
            return Clone(record);
        }

        bool sameDomain = !string.IsNullOrWhiteSpace(currentDomainSid) &&
                          record.SidDomainPrefix.Equals(currentDomainSid, StringComparison.OrdinalIgnoreCase);
        record.AccountName = sid.Value;
        record.DisplayName = "SID NO RESUELTO";
        record.PrincipalType = "Desconocido";

        if (sameDomain)
        {
            record.Origin = "DOMINIO ACTUAL, OBJETO NO ENCONTRADO";
            record.MatchedBy = "Prefijo SID del dominio";
            record.Status = "POSIBLE SID HUÉRFANO";
            record.Recommendation = "CANDIDATO A RETIRAR, solo después de validación del cliente, respaldo de ACL y revisión de todas las rutas afectadas.";
            record.Risk = "MEDIO";
            record.Evidence = "Prefijo del dominio actual, sin coincidencia en objectSid ni sIDHistory.";
        }
        else
        {
            record.Origin = "NO IDENTIFICADO";
            record.MatchedBy = "Sin coincidencia";
            record.Status = "SID NO RESUELTO";
            record.Recommendation = "REVISAR MANUALMENTE. No retirar automáticamente.";
            record.Risk = "ALTO";
            record.Evidence = "No se tradujo ni se localizó en objectSid o sIDHistory.";
        }

        cache[sid.Value] = record;
        return Clone(record);
    }

    public List<RidNeighbor> GetRidNeighbors(SecurityIdentifier sid, int radius = 3)
    {
        var list = new List<RidNeighbor>();
        string prefix = DomainPrefix(sid);
        int rid = Rid(sid);
        if (rid < 0 || !prefix.StartsWith("S-1-5-21-", StringComparison.OrdinalIgnoreCase))
            return list;

        for (int candidate = Math.Max(0, rid - radius); candidate <= rid + radius; candidate++)
        {
            if (candidate == rid) continue;
            string candidateText = prefix + "-" + candidate;
            var candidateSid = new SecurityIdentifier(candidateText);
            SearchResult? found = FindByBinarySid("objectSid", candidateSid);
            var neighbor = new RidNeighbor { Rid = candidate, Sid = candidateText, Exists = found is not null };
            if (found is not null)
            {
                var temp = new SidOriginRecord();
                FillFromAd(temp, found);
                neighbor.AccountName = temp.AccountName;
                neighbor.DisplayName = temp.DisplayName;
                neighbor.PrincipalType = temp.PrincipalType;
            }
            list.Add(neighbor);
        }
        return list;
    }

    private SearchResult? FindByBinarySid(string attribute, SecurityIdentifier sid)
    {
        try
        {
            using var searcher = new DirectorySearcher(searchRoot)
            {
                Filter = $"(&(objectClass=*)({attribute}={EscapeBinary(Bytes(sid))}))",
                SearchScope = SearchScope.Subtree,
                PageSize = 1000
            };
            foreach (string property in new[] { "displayName", "name", "sAMAccountName", "objectClass", "objectSid" })
                searcher.PropertiesToLoad.Add(property);
            return searcher.FindOne();
        }
        catch { return null; }
    }

    private static void FillFromAd(SidOriginRecord record, SearchResult result)
    {
        record.DisplayName = Property(result, "displayName");
        if (string.IsNullOrWhiteSpace(record.DisplayName)) record.DisplayName = Property(result, "name");
        string sam = Property(result, "sAMAccountName");
        record.AccountName = string.IsNullOrWhiteSpace(sam) ? Property(result, "name") : sam;
        record.PrincipalType = Type(result);
        if (result.Properties["objectSid"].Count > 0 && result.Properties["objectSid"][0] is byte[] bytes)
            record.CurrentObjectSid = new SecurityIdentifier(bytes, 0).Value;
    }

    private static string Type(SearchResult result)
    {
        foreach (object value in result.Properties["objectClass"])
        {
            string item = Convert.ToString(value) ?? "";
            if (item.Equals("group", StringComparison.OrdinalIgnoreCase)) return "Grupo";
            if (item.Equals("computer", StringComparison.OrdinalIgnoreCase)) return "Equipo";
            if (item.Equals("user", StringComparison.OrdinalIgnoreCase)) return "Usuario";
        }
        return "Objeto de Active Directory";
    }

    private static string Property(SearchResult result, string name) =>
        result.Properties[name].Count == 0 ? "" : Convert.ToString(result.Properties[name][0]) ?? "";
    private static byte[] Bytes(SecurityIdentifier sid) { byte[] b = new byte[sid.BinaryLength]; sid.GetBinaryForm(b, 0); return b; }
    private static string EscapeBinary(byte[] bytes) => string.Concat(bytes.Select(x => "\\" + x.ToString("X2")));
    private static string TryTranslate(SecurityIdentifier sid) { try { return ((NTAccount)sid.Translate(typeof(NTAccount))).Value; } catch { return ""; } }
    private static string DomainPrefix(SecurityIdentifier sid) { int i = sid.Value.LastIndexOf('-'); return i > 0 ? sid.Value[..i] : sid.Value; }
    private static int Rid(SecurityIdentifier sid) { string[] p = sid.Value.Split('-'); return int.TryParse(p[^1], out int value) ? value : -1; }
    private static bool IsBuiltIn(string a) => a.StartsWith("BUILTIN\\", StringComparison.OrdinalIgnoreCase) || a.StartsWith("NT AUTHORITY\\", StringComparison.OrdinalIgnoreCase) || a.StartsWith("CREATOR OWNER", StringComparison.OrdinalIgnoreCase);
    private static bool IsLocal(string a) { int i = a.IndexOf('\\'); return i > 0 && a[..i].Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase); }
    private static string InferType(string a) => IsBuiltIn(a) ? "Identidad integrada" : a.EndsWith("$") ? "Equipo" : "Cuenta";
    private static string Leaf(string a) { int i = a.LastIndexOf('\\'); return i >= 0 ? a[(i + 1)..] : a; }
    private static string ReadCurrentDomainSid(DirectoryEntry root) { try { root.RefreshCache(new[] { "objectSid" }); return root.Properties["objectSid"].Value is byte[] b ? new SecurityIdentifier(b, 0).Value : ""; } catch { return ""; } }
    private static SidOriginRecord Clone(SidOriginRecord s) => new() { Sid=s.Sid,Rid=s.Rid,SidDomainPrefix=s.SidDomainPrefix,AccountName=s.AccountName,DisplayName=s.DisplayName,PrincipalType=s.PrincipalType,Origin=s.Origin,MatchedBy=s.MatchedBy,CurrentObjectSid=s.CurrentObjectSid,CurrentDomainSid=s.CurrentDomainSid,Status=s.Status,Recommendation=s.Recommendation,Risk=s.Risk,Evidence=s.Evidence };
    public void Dispose() { searchRoot.Dispose(); rootDse.Dispose(); }
}
