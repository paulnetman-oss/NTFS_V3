using System.Security.Principal;

namespace NTFSSuite;

public static class SidForensicAnalyzer
{
    public static void Enrich(SidOriginRecord record, SidOriginResolver resolver)
    {
        record.AllowCount = record.Occurrences.Count(x => x.AccessType.Equals("Allow", StringComparison.OrdinalIgnoreCase));
        record.DenyCount = record.Occurrences.Count(x => x.AccessType.Equals("Deny", StringComparison.OrdinalIgnoreCase));
        record.InheritedCount = record.Occurrences.Count(x => x.Inherited);
        record.ExplicitCount = record.Occurrences.Count - record.InheritedCount;
        record.MainAreas = BuildMainAreas(record.Occurrences);
        record.RightsSummary = JoinCounts(record.Occurrences.Select(x => x.Rights));
        record.AccessTypesSummary = $"Allow: {record.AllowCount}; Deny: {record.DenyCount}; Heredadas: {record.InheritedCount}; Explícitas: {record.ExplicitCount}";

        if (record.Origin is "DOMINIO ACTUAL, OBJETO NO ENCONTRADO" or "NO IDENTIFICADO")
        {
            var sid = new SecurityIdentifier(record.Sid);
            record.Neighbors = resolver.GetRidNeighbors(sid, 3);
            record.NeighborSummary = string.Join(" | ", record.Neighbors.Where(x => x.Exists).Select(x => $"RID {x.Rid}: {x.PrincipalType} {x.AccountName}"));
            Estimate(record);
        }
        else
        {
            record.ProbableOrigin = record.Origin;
            record.ProbableType = record.PrincipalType;
            record.Confidence = "ALTA";
            record.ConfidenceBasis = "Identidad localizada mediante " + record.MatchedBy + ".";
        }
    }

    private static void Estimate(SidOriginRecord record)
    {
        var existing = record.Neighbors.Where(x => x.Exists).ToList();
        int users = existing.Count(x => x.PrincipalType == "Usuario");
        int groups = existing.Count(x => x.PrincipalType == "Grupo");
        int computers = existing.Count(x => x.PrincipalType == "Equipo");

        record.ProbableOrigin = record.Origin == "DOMINIO ACTUAL, OBJETO NO ENCONTRADO"
            ? "Objeto eliminado del dominio actual"
            : "Origen no demostrable con las fuentes consultadas";

        if (users > groups && users > computers)
            record.ProbableType = "Posible usuario eliminado";
        else if (groups > users && groups > computers)
            record.ProbableType = "Posible grupo eliminado";
        else if (computers > users && computers > groups)
            record.ProbableType = "Posible equipo eliminado";
        else
            record.ProbableType = "Tipo no determinable";

        if (record.Origin == "DOMINIO ACTUAL, OBJETO NO ENCONTRADO" && existing.Count >= 4)
            record.Confidence = "MEDIA";
        else
            record.Confidence = "BAJA";

        record.ConfidenceBasis =
            $"Sin objectSid ni sIDHistory. RID {record.Rid}. Vecinos vigentes consultados: {existing.Count}; usuarios: {users}; grupos: {groups}; equipos: {computers}. " +
            "La vecindad de RID es una pista contextual y no demuestra la identidad histórica.";
    }

    private static string BuildMainAreas(IEnumerable<SidOccurrence> items)
    {
        return string.Join(" | ", items
            .Select(x => TopArea(x.Path))
            .Where(x => x.Length > 0)
            .GroupBy(x => x, StringComparer.CurrentCultureIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Take(8)
            .Select(g => $"{g.Key} ({g.Count()})"));
    }

    private static string TopArea(string path)
    {
        string[] parts = path.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2) return parts[1];
        return parts.Length == 1 ? parts[0] : "";
    }

    private static string JoinCounts(IEnumerable<string> values) =>
        string.Join(" | ", values
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Select(g => $"{g.Key} ({g.Count()})"));
}
