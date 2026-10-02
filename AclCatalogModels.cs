namespace NTFSSuite;

public sealed class AclCatalogFile
{
    public string Version { get; set; } = "";
    public string Source { get; set; } = "";
    public List<AclCatalogRule> Rules { get; set; } = new();
    public List<AclCatalogRule> Removed { get; set; } = new();
}

public sealed class AclCatalogRule
{
    public string Id { get; set; } = "";
    public string AccessType { get; set; } = "";
    public string Scope { get; set; } = "";
    public bool Inherited { get; set; }
    public string Rights { get; set; } = "";
    public string Value { get; set; } = "";
}

public sealed class AclCatalogMatch
{
    public bool Found { get; set; }
    public string RuleId { get; set; } = "";
    public string Value { get; set; } = "R";
    public string Signature { get; set; } = "";
    public string Detail { get; set; } = "";
}
