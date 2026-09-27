using System.DirectoryServices;
using System.Security.Principal;

namespace NTFSSuite;

public sealed class PrincipalResolver
{
    private readonly Dictionary<string, PrincipalInfo> cache =
        new(StringComparer.OrdinalIgnoreCase);

    public PrincipalInfo Resolve(SecurityIdentifier sid)
    {
        if (cache.TryGetValue(sid.Value, out PrincipalInfo? cached))
            return cached;

        var info = new PrincipalInfo { Sid = sid.Value };

        try
        {
            info.AccountName = ((NTAccount)sid.Translate(typeof(NTAccount))).Value;
            info.IsResolved = true;
            info.ResolutionMethod = "Traducción SID a cuenta";
        }
        catch
        {
            info.AccountName = sid.Value;
            info.IsResolved = false;
            info.ResolutionMethod = "SID no traducido";
        }

        try
        {
            using var entry = new DirectoryEntry("LDAP://<SID=" + sid.Value + ">");
            entry.RefreshCache(new[] { "displayName", "name", "objectClass", "sAMAccountName" });

            info.DisplayName = Get(entry, "displayName");
            if (string.IsNullOrWhiteSpace(info.DisplayName))
                info.DisplayName = Get(entry, "name");

            string sam = Get(entry, "sAMAccountName");
            if (!string.IsNullOrWhiteSpace(sam) &&
                (string.IsNullOrWhiteSpace(info.AccountName) || info.AccountName == sid.Value))
                info.AccountName = sam;

            info.PrincipalType = GetPrincipalType(entry);
            info.IsResolved = true;
            info.ResolutionMethod = "Active Directory por SID";
        }
        catch
        {
            if (string.IsNullOrWhiteSpace(info.DisplayName) && info.IsResolved)
                info.DisplayName = AccountLeaf(info.AccountName);

            if (string.IsNullOrWhiteSpace(info.PrincipalType))
                info.PrincipalType = InferType(info.AccountName, sid);
        }

        if (string.IsNullOrWhiteSpace(info.DisplayName))
            info.DisplayName = info.IsResolved ? AccountLeaf(info.AccountName) : "SID NO RESUELTO";

        if (string.IsNullOrWhiteSpace(info.PrincipalType))
            info.PrincipalType = info.IsResolved ? "Cuenta" : "Desconocido";

        cache[sid.Value] = info;
        return info;
    }

    private static string Get(DirectoryEntry entry, string property)
    {
        object? value = entry.Properties[property].Value;
        return value is null ? "" : Convert.ToString(value) ?? "";
    }

    private static string GetPrincipalType(DirectoryEntry entry)
    {
        foreach (object value in entry.Properties["objectClass"])
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

    private static string InferType(string account, SecurityIdentifier sid)
    {
        if (sid.IsWellKnown(WellKnownSidType.LocalSystemSid)) return "Identidad integrada";
        if (account.StartsWith("BUILTIN\\", StringComparison.OrdinalIgnoreCase)) return "Grupo integrado";
        if (account.StartsWith("NT AUTHORITY\\", StringComparison.OrdinalIgnoreCase)) return "Identidad integrada";
        if (account.EndsWith("$", StringComparison.OrdinalIgnoreCase)) return "Equipo";
        return "Cuenta";
    }

    private static string AccountLeaf(string account)
    {
        int separator = account.LastIndexOf('\\');
        return separator >= 0 ? account[(separator + 1)..] : account;
    }
}
