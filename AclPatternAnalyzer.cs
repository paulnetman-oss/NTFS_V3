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
        List<DirectoryItem> directories =
            TemplateService.ReadDirectories(template, root, levels);

        var patterns = new Dictionary<string, AclPattern>(StringComparer.Ordinal);
        var examples = new List<AclExample>();
        var resolver = new PrincipalResolver();
        int failedAclReads = 0;
        int completed = 0;

        foreach (DirectoryItem directory in directories)
        {
            completed++;
            progress?.Invoke(
                completed * 100 / Math.Max(1, directories.Count),
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
                failedAclReads++;
                continue;
            }

            foreach (FileSystemAccessRule rule in security.GetAccessRules(
                         includeExplicit: true,
                         includeInherited: true,
                         targetType: typeof(SecurityIdentifier)))
            {
                if (rule.IdentityReference is not SecurityIdentifier sid)
                    continue;

                PrincipalInfo principal = resolver.Resolve(sid);
                string scope = Scope(rule);
                string rights = NormalizeRights(rule.FileSystemRights);
                string suggested = Suggest(rule);

                string signature =
                    $"{rule.AccessControlType}|{scope}|{rights}|Inherited={rule.IsInherited}";

                if (!patterns.TryGetValue(signature, out AclPattern? pattern))
                {
                    pattern = new AclPattern
                    {
                        Signature = signature,
                        Type = rule.AccessControlType.ToString(),
                        Scope = scope,
                        Rights = rights,
                        Suggested = suggested
                    };
                    patterns.Add(signature, pattern);
                }

                pattern.Frequency++;

                var example = new AclExample
                {
                    Signature = signature,
                    AccountName = principal.AccountName,
                    DisplayName = principal.DisplayName,
                    Sid = principal.Sid,
                    PrincipalType = principal.PrincipalType,
                    ResolutionMethod = principal.ResolutionMethod,
                    Path = directory.RelativePath,
                    AccessType = rule.AccessControlType.ToString(),
                    Scope = scope,
                    Rights = rights,
                    Inherited = rule.IsInherited,
                    Suggested = suggested
                };
                examples.Add(example);

                // Un ejemplo con cuenta válida siempre sustituye a un SID sin resolver.
                if (string.IsNullOrWhiteSpace(pattern.ExamplePrincipal) ||
                    (!pattern.ExampleResolved && principal.IsResolved))
                {
                    SetExample(pattern, principal, directory.RelativePath, rule.IsInherited);
                }
            }
        }

        int patternNumber = 1;
        foreach (AclPattern pattern in patterns.Values
                     .OrderByDescending(value => value.Frequency)
                     .ThenBy(value => value.Signature, StringComparer.Ordinal))
        {
            pattern.Id = "ACL" + patternNumber++.ToString("000");
        }

        Dictionary<string, string> signatureToId = patterns.Values
            .ToDictionary(value => value.Signature, value => value.Id);

        foreach (AclExample example in examples)
            example.PatternId = signatureToId[example.Signature];

        WriteReport(
            output,
            patterns.Values.OrderBy(value => value.Id).ToList(),
            examples,
            directories,
            failedAclReads);
    }

    private static void SetExample(
        AclPattern pattern,
        PrincipalInfo principal,
        string path,
        bool inherited)
    {
        pattern.ExamplePrincipal = principal.AccountName;
        pattern.ExampleDisplayName = principal.DisplayName;
        pattern.ExampleSid = principal.Sid;
        pattern.ExamplePrincipalType = principal.PrincipalType;
        pattern.ExampleResolution = principal.ResolutionMethod;
        pattern.ExamplePath = path;
        pattern.ExampleInherited = inherited;
        pattern.ExampleResolved = principal.IsResolved;
    }

    private static string NormalizeRights(FileSystemRights rights) =>
        (rights & ~FileSystemRights.Synchronize).ToString();

    private static string Scope(FileSystemAccessRule rule)
    {
        if (rule.InheritanceFlags ==
                (InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit) &&
            rule.PropagationFlags == PropagationFlags.None)
            return "Esta carpeta, subcarpetas y archivos";

        if (rule.InheritanceFlags == InheritanceFlags.None)
            return "Solo esta carpeta";

        return rule.InheritanceFlags + " / " + rule.PropagationFlags;
    }

    private static string Suggest(FileSystemAccessRule rule)
    {
        FileSystemRights normalized =
            rule.FileSystemRights & ~FileSystemRights.Synchronize;

        if (rule.AccessControlType == AccessControlType.Allow &&
            normalized == FileSystemRights.FullControl)
            return "LEM";

        if (rule.AccessControlType == AccessControlType.Allow &&
            (normalized & FileSystemRights.Modify) == FileSystemRights.Modify)
            return "M";

        if (rule.AccessControlType == AccessControlType.Allow &&
            (normalized & FileSystemRights.ReadAndExecute) == FileSystemRights.ReadAndExecute)
            return "L";

        return "SIN CLASIFICAR";
    }

    private static void WriteReport(
        string output,
        List<AclPattern> patterns,
        List<AclExample> examples,
        List<DirectoryItem> directories,
        int failedAclReads)
    {
        using var workbook = new XLWorkbook();

        WriteSummary(workbook, patterns, examples, directories, failedAclReads);
        WritePatterns(workbook, "COMBINACIONES DETECTADAS", patterns);
        WriteExamples(workbook, examples);
        WriteUnclassified(workbook, patterns);
        WriteUnresolved(workbook, examples);
        WriteRoutes(workbook, directories);

        workbook.SaveAs(output);
    }

    private static void WriteSummary(
        XLWorkbook workbook,
        List<AclPattern> patterns,
        List<AclExample> examples,
        List<DirectoryItem> directories,
        int failedAclReads)
    {
        var sheet = workbook.AddWorksheet("RESUMEN");
        Header(sheet, new[] { "Concepto", "Cantidad" });

        object[,] rows =
        {
            { "Directorios documentados", directories.Count },
            { "Directorios resueltos", directories.Count(value => value.FullPath.Length > 0) },
            { "Combinaciones únicas", patterns.Count },
            { "Entradas ACL analizadas", examples.Count },
            { "Principales resueltos", examples.Count(value => !value.DisplayName.Equals("SID NO RESUELTO", StringComparison.OrdinalIgnoreCase)) },
            { "Entradas con SID no resuelto", examples.Count(value => value.DisplayName.Equals("SID NO RESUELTO", StringComparison.OrdinalIgnoreCase)) },
            { "Directorios con error al leer ACL", failedAclReads }
        };

        for (int row = 0; row < rows.GetLength(0); row++)
        {
            sheet.Cell(row + 2, 1).Value = XLCellValue.FromObject(rows[row, 0]);
            sheet.Cell(row + 2, 2).Value = XLCellValue.FromObject(rows[row, 1]);
        }
        sheet.Columns().AdjustToContents();
    }

    private static void WritePatterns(
        XLWorkbook workbook,
        string sheetName,
        IEnumerable<AclPattern> patterns)
    {
        var sheet = workbook.AddWorksheet(sheetName);
        Header(sheet, new[]
        {
            "ID", "Frecuencia", "Permitir/Denegar", "Se aplica a",
            "Heredado ejemplo", "Derechos avanzados", "Clasificación sugerida",
            "Cuenta ejemplo", "DisplayName ejemplo", "Tipo principal",
            "SID ejemplo", "Método de resolución", "Ruta ejemplo"
        });

        int row = 2;
        foreach (AclPattern pattern in patterns)
        {
            object[] values =
            {
                pattern.Id,
                pattern.Frequency,
                pattern.Type,
                pattern.Scope,
                pattern.ExampleInherited ? "Sí" : "No",
                pattern.Rights,
                pattern.Suggested,
                pattern.ExamplePrincipal,
                pattern.ExampleDisplayName,
                pattern.ExamplePrincipalType,
                pattern.ExampleSid,
                pattern.ExampleResolution,
                pattern.ExamplePath
            };
            Put(sheet, row++, values);
        }

        FinishTable(sheet, 13);
    }

    private static void WriteExamples(
        XLWorkbook workbook,
        IEnumerable<AclExample> examples)
    {
        var sheet = workbook.AddWorksheet("EJEMPLOS");
        Header(sheet, new[]
        {
            "ID", "Cuenta", "DisplayName", "Tipo principal", "SID",
            "Método de resolución", "Ruta", "Permitir/Denegar",
            "Se aplica a", "Derechos avanzados", "Heredado",
            "Clasificación sugerida"
        });

        int row = 2;
        foreach (AclExample example in examples.OrderBy(value => value.PatternId))
        {
            object[] values =
            {
                example.PatternId,
                example.AccountName,
                example.DisplayName,
                example.PrincipalType,
                example.Sid,
                example.ResolutionMethod,
                example.Path,
                example.AccessType,
                example.Scope,
                example.Rights,
                example.Inherited ? "Sí" : "No",
                example.Suggested
            };
            Put(sheet, row++, values);
        }

        FinishTable(sheet, 12);
    }

    private static void WriteUnclassified(
        XLWorkbook workbook,
        IEnumerable<AclPattern> patterns)
    {
        WritePatterns(
            workbook,
            "SIN CLASIFICAR",
            patterns.Where(value => value.Suggested == "SIN CLASIFICAR"));
    }

    private static void WriteUnresolved(
        XLWorkbook workbook,
        IEnumerable<AclExample> examples)
    {
        var unresolved = examples
            .Where(value => value.DisplayName.Equals(
                "SID NO RESUELTO",
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        var sheet = workbook.AddWorksheet("SID NO RESUELTOS");
        Header(sheet, new[]
        {
            "ID", "SID", "Cuenta mostrada", "Tipo", "Ruta",
            "Permitir/Denegar", "Se aplica a", "Derechos avanzados", "Heredado"
        });

        int row = 2;
        foreach (AclExample example in unresolved)
        {
            object[] values =
            {
                example.PatternId,
                example.Sid,
                example.AccountName,
                example.PrincipalType,
                example.Path,
                example.AccessType,
                example.Scope,
                example.Rights,
                example.Inherited ? "Sí" : "No"
            };
            Put(sheet, row++, values);
        }

        FinishTable(sheet, 9);
    }

    private static void WriteRoutes(
        XLWorkbook workbook,
        IEnumerable<DirectoryItem> directories)
    {
        var sheet = workbook.AddWorksheet("RUTAS");
        Header(sheet, new[] { "Fila", "Ruta documentada", "Ruta física", "Estado" });

        int row = 2;
        foreach (DirectoryItem directory in directories)
        {
            Put(sheet, row++, new object[]
            {
                directory.Row,
                directory.RelativePath,
                directory.FullPath,
                directory.Resolution
            });
        }
        FinishTable(sheet, 4);
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
            sheet.Cell(row, index + 1).Value =
                XLCellValue.FromObject(values[index]);
    }

    private static void FinishTable(IXLWorksheet sheet, int columns)
    {
        sheet.SheetView.FreezeRows(1);
        if (sheet.LastRowUsed() is not null)
            sheet.Range(1, 1, sheet.LastRowUsed().RowNumber(), columns).SetAutoFilter();
        sheet.Columns(1, columns).AdjustToContents(12, 80);
    }
}
