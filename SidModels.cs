namespace NTFSSuite;

public sealed class SidOccurrence
{
    public string Sid { get; set; } = "";
    public string Path { get; set; } = "";
    public string PhysicalPath { get; set; } = "";
    public string AccessType { get; set; } = "";
    public string Rights { get; set; } = "";
    public string Scope { get; set; } = "";
    public bool Inherited { get; set; }
}

public sealed class SidOriginRecord
{
    public string Sid { get; set; } = "";
    public int Frequency { get; set; }
    public int DistinctPaths { get; set; }
    public string SidDomainPrefix { get; set; } = "";
    public string AccountName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string PrincipalType { get; set; } = "";
    public string Origin { get; set; } = "";
    public string MatchedBy { get; set; } = "";
    public string CurrentObjectSid { get; set; } = "";
    public string CurrentDomainSid { get; set; } = "";
    public string Status { get; set; } = "";
    public string Recommendation { get; set; } = "";
    public string Risk { get; set; } = "";
    public string Evidence { get; set; } = "";
    public List<SidOccurrence> Occurrences { get; set; } = new();
}
