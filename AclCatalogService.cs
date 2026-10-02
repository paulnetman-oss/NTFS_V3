using System.Security.AccessControl;
using System.Text.Json;

namespace NTFSSuite;

public sealed class AclCatalogService
{
    private readonly Dictionary<string, AclCatalogRule> rules;

    public string Version { get; }
    public string Source { get; }
    public int RuleCount => rules.Count;

    private AclCatalogService(AclCatalogFile catalog)
    {
        Version = catalog.Version;
        Source = catalog.Source;
        rules = catalog.Rules.ToDictionary(
            RuleSignature,
            value => value,
            StringComparer.OrdinalIgnoreCase);
    }

    public static AclCatalogService LoadDefault()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "CatalogoACL.json");
        if (!File.Exists(path))
            throw new FileNotFoundException(
                "No se encontró CatalogoACL.json junto al ejecutable.", path);

        string json = File.ReadAllText(path);
        var catalog = JsonSerializer.Deserialize<AclCatalogFile>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("El catálogo ACL no es válido.");

        return new AclCatalogService(catalog);
    }

    public AclCatalogMatch Match(FileSystemAccessRule rule)
    {
        string accessType = rule.AccessControlType.ToString();
        string scope = Scope(rule);
        string rights = NormalizeRights(rule.FileSystemRights);
        string signature = Signature(accessType, scope, rule.IsInherited, rights);

        if (rules.TryGetValue(signature, out AclCatalogRule? catalogRule))
        {
            return new AclCatalogMatch
            {
                Found = true,
                RuleId = catalogRule.Id,
                Value = catalogRule.Value,
                Signature = signature,
                Detail = $"Coincidencia con {catalogRule.Id}; valor autorizado: {catalogRule.Value}."
            };
        }

        return new AclCatalogMatch
        {
            Found = false,
            Value = "R",
            Signature = signature,
            Detail = "La combinación no existe en el catálogo aprobado; requiere revisión."
        };
    }

    public AclCatalogMatch Match(
        string accessType,
        string scope,
        bool inherited,
        string rights)
    {
        string signature = Signature(
            accessType,
            scope,
            inherited,
            NormalizeRightsText(rights));

        if (rules.TryGetValue(signature, out AclCatalogRule? rule))
            return new AclCatalogMatch
            {
                Found = true,
                RuleId = rule.Id,
                Value = rule.Value,
                Signature = signature,
                Detail = $"Coincidencia con {rule.Id}; valor autorizado: {rule.Value}."
            };

        return new AclCatalogMatch
        {
            Found = false,
            Value = "R",
            Signature = signature,
            Detail = "La combinación no existe en el catálogo aprobado; requiere revisión."
        };
    }

    public static string Scope(FileSystemAccessRule rule)
    {
        if (rule.InheritanceFlags ==
                (InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit) &&
            rule.PropagationFlags == PropagationFlags.None)
            return "Esta carpeta, subcarpetas y archivos";

        if (rule.InheritanceFlags == InheritanceFlags.None)
            return "Solo esta carpeta";

        return rule.InheritanceFlags + " / " + rule.PropagationFlags;
    }

    public static string NormalizeRights(FileSystemRights rights) =>
        NormalizeRightsText((rights & ~FileSystemRights.Synchronize).ToString());

    public static string NormalizeRightsText(string rights)
    {
        return string.Join(", ", (rights ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
    }

    private static string RuleSignature(AclCatalogRule rule) =>
        Signature(rule.AccessType, rule.Scope, rule.Inherited,
            NormalizeRightsText(rule.Rights));

    private static string Signature(
        string accessType,
        string scope,
        bool inherited,
        string rights) =>
        $"{accessType.Trim().ToUpperInvariant()}|" +
        $"{TextNormalizer.Key(scope)}|" +
        $"{(inherited ? "HEREDADO" : "EXPLICITO")}|" +
        $"{rights.ToUpperInvariant()}";
}
