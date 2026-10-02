using ClosedXML.Excel;
using System.Security.AccessControl;
using System.Security.Principal;

namespace NTFSSuite;

public static class AclPatternAnalyzer
{
    public static void Run(
        string template,
        string root,
        int levels,
        string output,
        Action<int, string>? progress = null)
    {
        var catalog = AclCatalogService.LoadDefault();
        List<DirectoryItem> directories =
            TemplateService.ReadDirectories(template, root, levels);

        var patterns = new Dictionary<string, AclPattern>(StringComparer.Ordinal);
        var examples = new List<AclExample>();
        var resolver = new PrincipalResolver();
        int completed = 0;
        int errors = 0;

        foreach (DirectoryItem directory in directories)
        {
            completed++;
            progress?.Invoke(
                completed * 85 / Math.Max(1, directories.Count),
                directory.RelativePath);

            if (string.IsNullOrWhiteSpace(directory.FullPath) ||
                !Directory.Exists(directory.FullPath))
                continue;

            DirectorySecurity security;
            try
            {
                security = FileSystemAclExtensions.GetAccessControl(
                    new DirectoryInfo(directory.FullPath),
                    AccessControlSections.Access);
            }
            catch
            {
                errors++;
                continue;
            }

            foreach (FileSystemAccessRule rule in security.GetAccessRules(
                         true, true, typeof(SecurityIdentifier)))
            {
                if (rule.IdentityReference is not SecurityIdentifier sid)
                    continue;

                PrincipalInfo principal = resolver.Resolve(sid);
                AclCatalogMatch match = catalog.Match(rule);
                string scope = AclCatalogService.Scope(rule);
                string rights = AclCatalogService.NormalizeRights(rule.FileSystemRights);
                string signature = match.Signature;

                // Las combinaciones eliminadas del catálogo no vuelven a recibir
                // un código histórico. Si reaparecen, se muestran como NUEVA/R.
                if (!patterns.TryGetValue(signature, out AclPattern? pattern))
                {
                    pattern = new AclPattern
                    {
                        Signature = signature,
                        Type = rule.AccessControlType.ToString(),
                        Scope = scope,
                        Rights = rights,
                        Suggested = match.Value,
                        CatalogId = match.RuleId,
                        CatalogStatus = match.Found ? "CATÁLOGO APROBADO" : "NO CATALOGADA"
                    };
                    patterns.Add(signature, pattern);
                }

                pattern.Frequency++;
                examples.Add(new AclExample
                {
                    Signature = signature,
                    AccountName = principal.AccountName,
                    DisplayName = principal.DisplayName,
                    Sid = principal.Sid,
                    PrincipalType = principal.PrincipalType,
                    ResolutionMethod = principal.ResolutionMethod,
                    IsResolved = principal.IsResolved,
                    Path = directory.RelativePath,
                    AccessType = rule.AccessControlType.ToString(),
                    Scope = scope,
                    Rights = rights,
                    Inherited = rule.IsInherited,
                    Suggested = match.Value,
                    CatalogId = match.RuleId,
                    CatalogStatus = match.Found ? "CATÁLOGO APROBADO" : "NO CATALOGADA"
                });
            }
        }

        SelectExamples(patterns.Values, examples);

        int newNumber = 1;
        foreach (AclPattern pattern in patterns.Values
                     .Where(value => string.IsNullOrWhiteSpace(value.CatalogId))
                     .OrderByDescending(value => value.Frequency))
            pattern.Id = "NUEVA" + newNumber++.ToString("000");

        foreach (AclPattern pattern in patterns.Values
                     .Where(value => !string.IsNullOrWhiteSpace(value.CatalogId)))
            pattern.Id = pattern.CatalogId;

        Dictionary<string, string> ids = patterns.Values
            .ToDictionary(value => value.Signature, value => value.Id);
        foreach (AclExample example in examples)
            example.PatternId = ids[example.Signature];

        progress?.Invoke(92, "Generando reporte con catálogo aprobado");
        WriteReport(output, patterns.Values.OrderBy(value => value.Id).ToList(),
            examples, directories, errors, catalog);
        progress?.Invoke(100, "Finalizado");
    }

    private static void SelectExamples(
        IEnumerable<AclPattern> patterns,
        List<AclExample> examples)
    {
        var grouped = examples.GroupBy(value => value.Signature)
            .ToDictionary(group => group.Key, group => group.ToList());

        foreach (AclPattern pattern in patterns)
        {
            List<AclExample> list = grouped[pattern.Signature];
            List<AclExample> distinct = list
                .GroupBy(value => value.Sid, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First()).ToList();

            pattern.DistinctPrincipals = distinct.Count;
            pattern.ResolvedPrincipals = distinct.Count(value => value.IsResolved);
            pattern.UnresolvedPrincipals = distinct.Count(value => !value.IsResolved);
            pattern.Example = list
                .OrderBy(value => value.IsResolved ? 0 : 1)
                .ThenBy(value => Priority(value.PrincipalType))
                .ThenBy(value => value.DisplayName)
                .First();
        }
    }

    private static int Priority(string type) => type switch
    {
        "Usuario" => 0,
        "Grupo" => 1,
        "Cuenta" => 2,
        "Grupo integrado" => 3,
        "Identidad integrada" => 4,
        "Equipo" => 5,
        _ => 6
    };

    private static void WriteReport(
        string output,
        List<AclPattern> patterns,
        List<AclExample> examples,
        List<DirectoryItem> directories,
        int errors,
        AclCatalogService catalog)
    {
        using var workbook = new XLWorkbook();
        var summary = workbook.AddWorksheet("RESUMEN");
        Header(summary, new[] { "Concepto", "Cantidad / Valor" });
        object[,] rows =
        {
            { "Versión de catálogo", catalog.Version },
            { "Reglas aprobadas", catalog.RuleCount },
            { "Directorios documentados", directories.Count },
            { "Directorios resueltos", directories.Count(value => value.FullPath.Length > 0) },
            { "Combinaciones detectadas", patterns.Count },
            { "Combinaciones catalogadas", patterns.Count(value => value.CatalogStatus == "CATÁLOGO APROBADO") },
            { "Combinaciones nuevas", patterns.Count(value => value.CatalogStatus == "NO CATALOGADA") },
            { "Entradas ACL analizadas", examples.Count },
            { "Errores ACL", errors }
        };
        for (int row = 0; row < rows.GetLength(0); row++)
            Put(summary, row + 2, new[] { rows[row, 0], rows[row, 1] });
        Finish(summary, 2);

        WritePatterns(workbook, "COMBINACIONES DETECTADAS", patterns);
        WritePatterns(workbook, "NUEVAS COMBINACIONES",
            patterns.Where(value => value.CatalogStatus == "NO CATALOGADA"));
        WriteExamples(workbook, examples);

        var routes = workbook.AddWorksheet("RUTAS");
        Header(routes, new[] { "Fila", "Ruta documentada", "Ruta física", "Estado" });
        int routeRow = 2;
        foreach (DirectoryItem directory in directories)
            Put(routes, routeRow++, new object[]
            {
                directory.Row, directory.RelativePath,
                directory.FullPath, directory.Resolution
            });
        Finish(routes, 4);
        workbook.SaveAs(output);
    }

    private static void WritePatterns(
        XLWorkbook workbook,
        string name,
        IEnumerable<AclPattern> patterns)
    {
        var sheet = workbook.AddWorksheet(name);
        Header(sheet, new[]
        {
            "ID", "Valor aplicado", "Estado catálogo", "Frecuencia",
            "Principales distintos", "Resueltos", "SID no resueltos",
            "Permitir/Denegar", "Se aplica a", "Heredado",
            "Derechos avanzados", "Cuenta ejemplo", "DisplayName ejemplo",
            "Tipo principal", "Ruta ejemplo", "SID técnico"
        });
        int row = 2;
        foreach (AclPattern pattern in patterns)
        {
            AclExample example = pattern.Example!;
            Put(sheet, row, new object[]
            {
                pattern.Id, pattern.Suggested, pattern.CatalogStatus,
                pattern.Frequency, pattern.DistinctPrincipals,
                pattern.ResolvedPrincipals, pattern.UnresolvedPrincipals,
                pattern.Type, pattern.Scope, example.Inherited ? "Sí" : "No",
                pattern.Rights,
                example.IsResolved ? example.AccountName : "Sin cuenta vigente",
                example.IsResolved ? example.DisplayName : "SID NO RESUELTO",
                example.PrincipalType, example.Path, example.Sid
            });
            if (pattern.CatalogStatus == "NO CATALOGADA")
                sheet.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF2CC");
            row++;
        }
        Finish(sheet, 16);
    }

    private static void WriteExamples(
        XLWorkbook workbook,
        IEnumerable<AclExample> examples)
    {
        var sheet = workbook.AddWorksheet("EJEMPLOS");
        Header(sheet, new[]
        {
            "ID", "Valor aplicado", "Estado catálogo", "Estado identidad",
            "Cuenta", "DisplayName", "Tipo", "Ruta", "Permitir/Denegar",
            "Se aplica a", "Derechos", "Heredado", "SID técnico"
        });
        int row = 2;
        foreach (AclExample example in examples
                     .OrderBy(value => value.PatternId)
                     .ThenBy(value => value.IsResolved ? 0 : 1))
            Put(sheet, row++, new object[]
            {
                example.PatternId, example.Suggested, example.CatalogStatus,
                example.IsResolved ? "RESUELTO" : "NO RESUELTO",
                example.IsResolved ? example.AccountName : "Sin cuenta vigente",
                example.IsResolved ? example.DisplayName : "SID NO RESUELTO",
                example.PrincipalType, example.Path, example.AccessType,
                example.Scope, example.Rights,
                example.Inherited ? "Sí" : "No", example.Sid
            });
        Finish(sheet, 13);
    }

    private static void Header(IXLWorksheet sheet, string[] headers)
    {
        for (int index = 0; index < headers.Length; index++)
        {
            IXLCell cell = sheet.Cell(1, index + 1);
            cell.Value = headers[index];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E78");
        }
    }

    private static void Put(IXLWorksheet sheet, int row, object[] values)
    {
        for (int index = 0; index < values.Length; index++)
            sheet.Cell(row, index + 1).Value = XLCellValue.FromObject(values[index]);
    }

    private static void Finish(IXLWorksheet sheet, int columns)
    {
        sheet.SheetView.FreezeRows(1);
        if (sheet.LastRowUsed() is not null)
            sheet.Range(1, 1, sheet.LastRowUsed().RowNumber(), columns).SetAutoFilter();
        sheet.Columns(1, columns).AdjustToContents(12, 85);
    }
}
