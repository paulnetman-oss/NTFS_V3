namespace NTFSSuite;

public sealed class DirectoryItem
{
    public int Row { get; set; }
    public string RelativePath { get; set; } = "";
    public string FullPath { get; set; } = "";
    public string[] Levels { get; set; } = Array.Empty<string>();
    public string Resolution { get; set; } = "";
}

public sealed class PrincipalInfo
{
    public string Sid { get; set; } = "";
    public string AccountName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string PrincipalType { get; set; } = "";
    public bool IsResolved { get; set; }
    public string ResolutionMethod { get; set; } = "";
}

public sealed class AclPattern
{
    public string Id { get; set; } = "";
    public string Signature { get; set; } = "";
    public string Type { get; set; } = "";
    public string Scope { get; set; } = "";
    public string Rights { get; set; } = "";
    public string Suggested { get; set; } = "";
    public int Frequency { get; set; }

    public string ExamplePrincipal { get; set; } = "";
    public string ExampleDisplayName { get; set; } = "";
    public string ExampleSid { get; set; } = "";
    public string ExamplePrincipalType { get; set; } = "";
    public string ExampleResolution { get; set; } = "";
    public string ExamplePath { get; set; } = "";
    public bool ExampleInherited { get; set; }
    public bool ExampleResolved { get; set; }
}

public sealed class AclExample
{
    public string PatternId { get; set; } = "";
    public string Signature { get; set; } = "";
    public string AccountName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Sid { get; set; } = "";
    public string PrincipalType { get; set; } = "";
    public string ResolutionMethod { get; set; } = "";
    public string Path { get; set; } = "";
    public string AccessType { get; set; } = "";
    public string Scope { get; set; } = "";
    public string Rights { get; set; } = "";
    public bool Inherited { get; set; }
    public string Suggested { get; set; } = "";
}

public sealed class ChangePlanItem
{
    public string Cell { get; set; } = "";
    public string Path { get; set; } = "";
    public string FriendlyName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Requested { get; set; } = "";
    public string Status { get; set; } = "";
    public string Notes { get; set; } = "";
}
