using ClosedXML.Excel;
using System.Security.AccessControl;
using System.Security.Principal;

namespace NTFSSuite;

public static class SidOriginAnalyzer
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

        var occurrences = new Dictionary<string, List<SidOccurrence>>(
            StringComparer.OrdinalIgnoreCase);
        int failedAclReads = 0;
        int completed = 0;

        foreach (DirectoryItem directory in directories)
        {
            completed++;
            progress?.Invoke(
                completed * 45 / Math.Max(1, directories.Count),
                "Leyendo ACL: " + directory.RelativePath);

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

                if (!occurrences.TryGetValue(sid.Value, out List<SidOccurrence>? list))
                {
                    list = new List<SidOccurrence>();
                    occurrences.Add(sid.Value, list);
                }

                list.Add(new SidOccurrence
                {
                    Sid = sid.Value,
                    Path = directory.RelativePath,
                    PhysicalPath = directory.FullPath,
                    AccessType = rule.AccessControlType.ToString(),
                    Rights = NormalizeRights(rule.FileSystemRights),
                    Scope = Scope(rule),
                    Inherited = rule.IsInherited
                });
            }
        }

        var records = new List<SidOriginRecord>();
        using var resolver = new SidOriginResolver();
        int index = 0;

        foreach ((string sidText, List<SidOccurrence> sidOccurrences) in occurrences)
        {
            index++;
            progress?.Invoke(
                45 + index * 45 / Math.Max(1, occurrences.Count),
                "Resolviendo SID: " + sidText);

            var sid = new SecurityIdentifier(sidText);
            SidOriginRecord record = resolver.Resolve(sid);
            record.Occurrences = sidOccurrences;
            record.Frequency = sidOccurrences.Count;
            record.DistinctPaths = sidOccurrences
                .Select(value => value.Path)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            records.Add(record);
        }

        progress?.Invoke(92, "Generando libro de origen de SID");
        WriteReport(
            output,
            records.OrderBy(value => OriginOrder(value.Origin))
                .ThenByDescending(value => value.Frequency)
                .ThenBy(value => value.Sid, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            directories,
            failedAclReads);
        progress?.Invoke(100, "Finalizado");
    }

    private static int OriginOrder(string origin) => origin switch
    {
        "ACTIVE DIRECTORY ACTUAL" => 0,
        "IDENTIDAD INTEGRADA DE WINDOWS" => 1,
        "CUENTA O GRUPO LOCAL" => 2,
        "SID HISTORY" => 3,
        "DOMINIO EXTERNO O CONTEXTO NO CONSULTADO" => 4,
        "DOMINIO ACTUAL, OBJETO NO ENCONTRADO" => 5,
        _ => 6
    };

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

    private static void WriteReport(
        string output,
        List<SidOriginRecord> records,
        List<DirectoryItem> directories,
        int failedAclReads)
    {
        using var workbook = new XLWorkbook();

        WriteSummary(workbook, records, directories, failedAclReads);
        WriteOrigins(workbook, records);
        WriteAffectedPaths(workbook, records);
        WriteActions(workbook, records);
        WriteOrphans(workbook, records);
        WriteHistory(workbook, records);
        WriteRoutes(workbook, directories);

        workbook.SaveAs(output);
    }

    private static void WriteSummary(
        XLWorkbook workbook,
        List<SidOriginRecord> records,
        List<DirectoryItem> directories,
        int failedAclReads)
    {
        var sheet = workbook.AddWorksheet("RESUMEN SID");
        Header(sheet, new[] { "Concepto", "Cantidad" });

        object[,] rows =
        {
            { "SID distintos encontrados", records.Count },
            { "Active Directory actual", records.Count(x => x.Origin == "ACTIVE DIRECTORY ACTUAL") },
            { "SID History", records.Count(x => x.Origin == "SID HISTORY") },
            { "Identidades integradas", records.Count(x => x.Origin == "IDENTIDAD INTEGRADA DE WINDOWS") },
            { "Cuentas o grupos locales", records.Count(x => x.Origin == "CUENTA O GRUPO LOCAL") },
            { "Dominio externo o contexto no consultado", records.Count(x => x.Origin == "DOMINIO EXTERNO O CONTEXTO NO CONSULTADO") },
            { "Posibles SID huérfanos del dominio actual", records.Count(x => x.Origin == "DOMINIO ACTUAL, OBJETO NO ENCONTRADO") },
            { "SID no identificados", records.Count(x => x.Origin == "NO IDENTIFICADO") },
            { "Entradas ACL analizadas", records.Sum(x => x.Frequency) },
            { "Directorios documentados", directories.Count },
            { "Directorios resueltos", directories.Count(x => x.FullPath.Length > 0) },
            { "Errores al leer ACL", failedAclReads }
        };

        for (int row = 0; row < rows.GetLength(0); row++)
        {
            sheet.Cell(row + 2, 1).Value = XLCellValue.FromObject(rows[row, 0]);
            sheet.Cell(row + 2, 2).Value = XLCellValue.FromObject(rows[row, 1]);
        }
        sheet.Columns().AdjustToContents();
    }

    private static void WriteOrigins(
        XLWorkbook workbook,
        IEnumerable<SidOriginRecord> records)
    {
        var sheet = workbook.AddWorksheet("ORIGEN SID");
        Header(sheet, new[]
        {
            "SID", "Frecuencia", "Rutas distintas", "Prefijo de dominio SID",
            "Origen", "Coincidencia", "Cuenta actual", "DisplayName", "Tipo",
            "SID actual del objeto", "SID del dominio actual", "Estado",
            "Recomendación", "Riesgo", "Evidencia"
        });

        int row = 2;
        foreach (SidOriginRecord record in records)
        {
            Put(sheet, row, new object[]
            {
                record.Sid,
                record.Frequency,
                record.DistinctPaths,
                record.SidDomainPrefix,
                record.Origin,
                record.MatchedBy,
                record.AccountName,
                record.DisplayName,
                record.PrincipalType,
                record.CurrentObjectSid,
                record.CurrentDomainSid,
                record.Status,
                record.Recommendation,
                record.Risk,
                record.Evidence
            });
            sheet.Row(row).Style.Fill.BackgroundColor =
                XLColor.FromHtml(OriginColor(record.Origin));
            row++;
        }

        Finish(sheet, 15);
    }

    private static void WriteAffectedPaths(
        XLWorkbook workbook,
        IEnumerable<SidOriginRecord> records)
    {
        var sheet = workbook.AddWorksheet("RUTAS AFECTADAS");
        Header(sheet, new[]
        {
            "SID", "Origen", "Cuenta", "DisplayName", "Ruta documentada",
            "Ruta física", "Permitir/Denegar", "Derechos", "Se aplica a", "Heredado"
        });

        int row = 2;
        foreach (SidOriginRecord record in records)
        {
            foreach (SidOccurrence occurrence in record.Occurrences)
            {
                Put(sheet, row++, new object[]
                {
                    record.Sid,
                    record.Origin,
                    record.AccountName,
                    record.DisplayName,
                    occurrence.Path,
                    occurrence.PhysicalPath,
                    occurrence.AccessType,
                    occurrence.Rights,
                    occurrence.Scope,
                    occurrence.Inherited ? "Sí" : "No"
                });
            }
        }

        Finish(sheet, 10);
    }

    private static void WriteActions(
        XLWorkbook workbook,
        IEnumerable<SidOriginRecord> records)
    {
        var sheet = workbook.AddWorksheet("ACCIONES PROPUESTAS");
        Header(sheet, new[]
        {
            "SID", "Origen", "Cuenta/Identidad", "Estado", "Frecuencia",
            "Rutas distintas", "Acción sugerida", "Riesgo", "Justificación",
            "Aprobación del cliente", "Resultado de revisión"
        });

        int row = 2;
        foreach (SidOriginRecord record in records)
        {
            Put(sheet, row, new object[]
            {
                record.Sid,
                record.Origin,
                string.IsNullOrWhiteSpace(record.DisplayName)
                    ? record.AccountName
                    : record.DisplayName,
                record.Status,
                record.Frequency,
                record.DistinctPaths,
                record.Recommendation,
                record.Risk,
                record.Evidence,
                "PENDIENTE",
                ""
            });
            sheet.Row(row).Style.Fill.BackgroundColor =
                XLColor.FromHtml(RiskColor(record.Risk));
            row++;
        }

        Finish(sheet, 11);
    }

    private static void WriteOrphans(
        XLWorkbook workbook,
        IEnumerable<SidOriginRecord> records)
    {
        WriteOrigins(
            workbook,
            records.Where(x =>
                x.Origin == "DOMINIO ACTUAL, OBJETO NO ENCONTRADO" ||
                x.Origin == "NO IDENTIFICADO"),
            "CANDIDATOS HUÉRFANOS");
    }

    private static void WriteHistory(
        XLWorkbook workbook,
        IEnumerable<SidOriginRecord> records)
    {
        WriteOrigins(
            workbook,
            records.Where(x => x.Origin == "SID HISTORY"),
            "SID HISTORY");
    }

    private static void WriteOrigins(
        XLWorkbook workbook,
        IEnumerable<SidOriginRecord> records,
        string sheetName)
    {
        var sheet = workbook.AddWorksheet(sheetName);
        Header(sheet, new[]
        {
            "SID encontrado", "Cuenta actual", "DisplayName", "Tipo",
            "SID actual", "Estado", "Frecuencia", "Rutas distintas",
            "Recomendación", "Riesgo", "Evidencia"
        });

        int row = 2;
        foreach (SidOriginRecord record in records)
        {
            Put(sheet, row++, new object[]
            {
                record.Sid,
                record.AccountName,
                record.DisplayName,
                record.PrincipalType,
                record.CurrentObjectSid,
                record.Status,
                record.Frequency,
                record.DistinctPaths,
                record.Recommendation,
                record.Risk,
                record.Evidence
            });
        }
        Finish(sheet, 11);
    }

    private static void WriteRoutes(
        XLWorkbook workbook,
        IEnumerable<DirectoryItem> directories)
    {
        var sheet = workbook.AddWorksheet("RUTAS ANALIZADAS");
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
        Finish(sheet, 4);
    }

    private static string OriginColor(string origin) => origin switch
    {
        "ACTIVE DIRECTORY ACTUAL" => "#E2F0D9",
        "IDENTIDAD INTEGRADA DE WINDOWS" => "#D9EAF7",
        "CUENTA O GRUPO LOCAL" => "#FFF2CC",
        "SID HISTORY" => "#E4DFEC",
        "DOMINIO EXTERNO O CONTEXTO NO CONSULTADO" => "#FCE4D6",
        "DOMINIO ACTUAL, OBJETO NO ENCONTRADO" => "#F4CCCC",
        _ => "#F4CCCC"
    };

    private static string RiskColor(string risk) => risk switch
    {
        "BAJO" => "#E2F0D9",
        "MEDIO" => "#FFF2CC",
        "ALTO" => "#F4CCCC",
        _ => "#FFFFFF"
    };

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

    private static void Finish(IXLWorksheet sheet, int columns)
    {
        sheet.SheetView.FreezeRows(1);
        if (sheet.LastRowUsed() is not null)
            sheet.Range(1, 1, sheet.LastRowUsed().RowNumber(), columns)
                .SetAutoFilter();
        sheet.Columns(1, columns).AdjustToContents(12, 85);
        sheet.Rows().Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        sheet.Rows().Style.Alignment.WrapText = true;
    }
}
