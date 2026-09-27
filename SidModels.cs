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

public sealed class RidNeighbor
{
    public int Rid { get; set; }
    public string Sid { get; set; } = "";
    public string AccountName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string PrincipalType { get; set; } = "";
    public bool Exists { get; set; }
}

public sealed class SidOriginRecord
{
    public string Sid { get; set; } = "";
    public int Rid { get; set; }
    public string SidDomainPrefix { get; set; } = "";
    public int Frequency { get; set; }
    public int DistinctPaths { get; set; }
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

    public string ProbableOrigin { get; set; } = "";
    public string ProbableType { get; set; } = "";
    public string Confidence { get; set; } = "";
    public string ConfidenceBasis { get; set; } = "";
    public string MainAreas { get; set; } = "";
    public string RightsSummary { get; set; } = "";
    public string AccessTypesSummary { get; set; } = "";
    public int AllowCount { get; set; }
    public int DenyCount { get; set; }
    public int InheritedCount { get; set; }
    public int ExplicitCount { get; set; }
    public string NeighborSummary { get; set; } = "";

    public List<SidOccurrence> Occurrences { get; set; } = new();
    public List<RidNeighbor> Neighbors { get; set; } = new();
}
