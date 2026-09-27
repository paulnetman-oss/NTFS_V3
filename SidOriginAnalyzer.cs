using ClosedXML.Excel;
using System.Security.AccessControl;
using System.Security.Principal;

namespace NTFSSuite;

public static class SidOriginAnalyzer
{
    public static void Run(string template,string root,int levels,string output,Action<int,string>? progress=null)
    {
        List<DirectoryItem> directories=TemplateService.ReadDirectories(template,root,levels);
        var found=new Dictionary<string,List<SidOccurrence>>(StringComparer.OrdinalIgnoreCase);
        int errors=0,done=0;

        foreach(DirectoryItem directory in directories)
        {
            done++;
            progress?.Invoke(done*40/Math.Max(1,directories.Count),"Leyendo ACL: "+directory.RelativePath);
            if(string.IsNullOrWhiteSpace(directory.FullPath)||!Directory.Exists(directory.FullPath))continue;
            DirectorySecurity security;
            try{security=FileSystemAclExtensions.GetAccessControl(new DirectoryInfo(directory.FullPath),AccessControlSections.Access);}catch{errors++;continue;}
            foreach(FileSystemAccessRule rule in security.GetAccessRules(true,true,typeof(SecurityIdentifier)))
            {
                if(rule.IdentityReference is not SecurityIdentifier sid)continue;
                if(!found.TryGetValue(sid.Value,out List<SidOccurrence>? list)){list=new();found.Add(sid.Value,list);}
                list.Add(new SidOccurrence{Sid=sid.Value,Path=directory.RelativePath,PhysicalPath=directory.FullPath,AccessType=rule.AccessControlType.ToString(),Rights=(rule.FileSystemRights&~FileSystemRights.Synchronize).ToString(),Scope=Scope(rule),Inherited=rule.IsInherited});
            }
        }

        var records=new List<SidOriginRecord>();
        using var resolver=new SidOriginResolver();
        int index=0;
        foreach(var pair in found)
        {
            index++;
            progress?.Invoke(40+index*50/Math.Max(1,found.Count),"Investigando SID: "+pair.Key);
            var record=resolver.Resolve(new SecurityIdentifier(pair.Key));
            record.Occurrences=pair.Value;
            record.Frequency=pair.Value.Count;
            record.DistinctPaths=pair.Value.Select(x=>x.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            SidForensicAnalyzer.Enrich(record,resolver);
            records.Add(record);
        }

        progress?.Invoke(94,"Generando investigación forense");
        Write(output,records.OrderBy(x=>Order(x.Origin)).ThenByDescending(x=>x.Frequency).ToList(),directories,errors);
        progress?.Invoke(100,"Finalizado");
    }

    private static string Scope(FileSystemAccessRule rule)=>
        rule.InheritanceFlags==(InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit)&&rule.PropagationFlags==PropagationFlags.None
            ?"Esta carpeta, subcarpetas y archivos"
            :rule.InheritanceFlags==InheritanceFlags.None?"Solo esta carpeta":rule.InheritanceFlags+" / "+rule.PropagationFlags;

    private static int Order(string origin)=>origin switch{"ACTIVE DIRECTORY ACTUAL"=>0,"IDENTIDAD INTEGRADA DE WINDOWS"=>1,"CUENTA O GRUPO LOCAL"=>2,"SID HISTORY"=>3,"DOMINIO EXTERNO O CONTEXTO NO CONSULTADO"=>4,"DOMINIO ACTUAL, OBJETO NO ENCONTRADO"=>5,_=>6};

    private static void Write(string output,List<SidOriginRecord> records,List<DirectoryItem> dirs,int errors)
    {
        using var wb=new XLWorkbook();
        Summary(wb,records,dirs,errors);
        Investigation(wb,records);
        Origins(wb,records);
        Affected(wb,records);
        Actions(wb,records);
        NeighborSheet(wb,records);
        Filtered(wb,"CANDIDATOS HUÉRFANOS",records.Where(x=>x.Origin is "DOMINIO ACTUAL, OBJETO NO ENCONTRADO" or "NO IDENTIFICADO"));
        Filtered(wb,"SID HISTORY",records.Where(x=>x.Origin=="SID HISTORY"));
        Routes(wb,dirs);
        wb.SaveAs(output);
    }

    private static void Summary(XLWorkbook wb,List<SidOriginRecord> r,List<DirectoryItem> d,int errors)
    {
        var s=wb.AddWorksheet("RESUMEN SID");Header(s,new[]{"Concepto","Cantidad"});
        object[,] rows={{"SID distintos",r.Count},{"AD actual",r.Count(x=>x.Origin=="ACTIVE DIRECTORY ACTUAL")},{"SID History",r.Count(x=>x.Origin=="SID HISTORY")},{"Integrados",r.Count(x=>x.Origin=="IDENTIDAD INTEGRADA DE WINDOWS")},{"Locales",r.Count(x=>x.Origin=="CUENTA O GRUPO LOCAL")},{"Posibles huérfanos",r.Count(x=>x.Origin=="DOMINIO ACTUAL, OBJETO NO ENCONTRADO")},{"No identificados",r.Count(x=>x.Origin=="NO IDENTIFICADO")},{"Entradas ACL",r.Sum(x=>x.Frequency)},{"Directorios resueltos",d.Count(x=>x.FullPath.Length>0)},{"Errores ACL",errors}};
        for(int i=0;i<rows.GetLength(0);i++)Put(s,i+2,new[]{rows[i,0],rows[i,1]});Finish(s,2);
    }

    private static void Investigation(XLWorkbook wb,IEnumerable<SidOriginRecord> records)
    {
        var s=wb.AddWorksheet("INVESTIGACIÓN FORENSE SID");Header(s,new[]{"SID","RID","Origen comprobado","Origen probable","Tipo comprobado","Tipo probable","Confianza","Base de confianza","Frecuencia","Rutas distintas","Áreas principales","Permisos encontrados","Resumen ACL","Vecinos RID vigentes","Estado","Acción sugerida","Riesgo","Evidencia"});int row=2;
        foreach(var x in records)
        {
            Put(s,row,new object[]{x.Sid,x.Rid,x.Origin,x.ProbableOrigin,x.PrincipalType,x.ProbableType,x.Confidence,x.ConfidenceBasis,x.Frequency,x.DistinctPaths,x.MainAreas,x.RightsSummary,x.AccessTypesSummary,x.NeighborSummary,x.Status,x.Recommendation,x.Risk,x.Evidence});
            s.Row(row).Style.Fill.BackgroundColor=XLColor.FromHtml(x.Confidence=="ALTA"?"#E2F0D9":x.Confidence=="MEDIA"?"#FFF2CC":"#FCE4D6");row++;
        }
        Finish(s,18);
    }

    private static void Origins(XLWorkbook wb,IEnumerable<SidOriginRecord> records)
    {
        var s=wb.AddWorksheet("ORIGEN SID");Header(s,new[]{"SID","Frecuencia","Rutas","Origen","Coincidencia","Cuenta","DisplayName","Tipo","SID actual","Estado","Recomendación","Riesgo","Evidencia"});int row=2;
        foreach(var x in records)Put(s,row++,new object[]{x.Sid,x.Frequency,x.DistinctPaths,x.Origin,x.MatchedBy,x.AccountName,x.DisplayName,x.PrincipalType,x.CurrentObjectSid,x.Status,x.Recommendation,x.Risk,x.Evidence});Finish(s,13);
    }

    private static void Affected(XLWorkbook wb,IEnumerable<SidOriginRecord> records)
    {
        var s=wb.AddWorksheet("RUTAS AFECTADAS");Header(s,new[]{"SID","Origen","Tipo probable","Ruta documentada","Ruta física","Allow/Deny","Derechos","Alcance","Heredado"});int row=2;
        foreach(var x in records)foreach(var o in x.Occurrences)Put(s,row++,new object[]{x.Sid,x.Origin,x.ProbableType,o.Path,o.PhysicalPath,o.AccessType,o.Rights,o.Scope,o.Inherited?"Sí":"No"});Finish(s,9);
    }

    private static void Actions(XLWorkbook wb,IEnumerable<SidOriginRecord> records)
    {
        var s=wb.AddWorksheet("ACCIONES PROPUESTAS");Header(s,new[]{"SID","Origen probable","Tipo probable","Confianza","Frecuencia","Rutas","Acción sugerida","Riesgo","Justificación","Aprobación cliente","Resultado revisión"});int row=2;
        foreach(var x in records){Put(s,row,new object[]{x.Sid,x.ProbableOrigin,x.ProbableType,x.Confidence,x.Frequency,x.DistinctPaths,x.Recommendation,x.Risk,x.ConfidenceBasis+" "+x.Evidence,"PENDIENTE",""});s.Row(row).Style.Fill.BackgroundColor=XLColor.FromHtml(x.Risk=="BAJO"?"#E2F0D9":x.Risk=="MEDIO"?"#FFF2CC":"#F4CCCC");row++;}Finish(s,11);
    }

    private static void NeighborSheet(XLWorkbook wb,IEnumerable<SidOriginRecord> records)
    {
        var s=wb.AddWorksheet("CONTEXTO RID");Header(s,new[]{"SID investigado","RID investigado","RID vecino","SID vecino","Existe","Cuenta vecina","DisplayName vecino","Tipo vecino","Nota"});int row=2;
        foreach(var x in records)foreach(var n in x.Neighbors)Put(s,row++,new object[]{x.Sid,x.Rid,n.Rid,n.Sid,n.Exists?"Sí":"No",n.AccountName,n.DisplayName,n.PrincipalType,"La vecindad RID es contexto, no prueba de identidad histórica."});Finish(s,9);
    }

    private static void Filtered(XLWorkbook wb,string name,IEnumerable<SidOriginRecord> records)
    {
        var s=wb.AddWorksheet(name);Header(s,new[]{"SID","RID","Origen probable","Tipo probable","Confianza","Frecuencia","Rutas","Áreas principales","Permisos","Recomendación","Riesgo","Evidencia"});int row=2;
        foreach(var x in records)Put(s,row++,new object[]{x.Sid,x.Rid,x.ProbableOrigin,x.ProbableType,x.Confidence,x.Frequency,x.DistinctPaths,x.MainAreas,x.RightsSummary,x.Recommendation,x.Risk,x.ConfidenceBasis});Finish(s,12);
    }

    private static void Routes(XLWorkbook wb,IEnumerable<DirectoryItem> dirs){var s=wb.AddWorksheet("RUTAS ANALIZADAS");Header(s,new[]{"Fila","Ruta documentada","Ruta física","Estado"});int row=2;foreach(var d in dirs)Put(s,row++,new object[]{d.Row,d.RelativePath,d.FullPath,d.Resolution});Finish(s,4);}
    private static void Header(IXLWorksheet s,string[] h){for(int i=0;i<h.Length;i++){var c=s.Cell(1,i+1);c.Value=h[i];c.Style.Font.Bold=true;c.Style.Font.FontColor=XLColor.White;c.Style.Fill.BackgroundColor=XLColor.FromHtml("#1F4E78");}}
    private static void Put(IXLWorksheet s,int row,object[] values){for(int i=0;i<values.Length;i++)s.Cell(row,i+1).Value=XLCellValue.FromObject(values[i]);}
    private static void Finish(IXLWorksheet s,int cols){s.SheetView.FreezeRows(1);if(s.LastRowUsed()!=null)s.Range(1,1,s.LastRowUsed().RowNumber(),cols).SetAutoFilter();s.Columns(1,cols).AdjustToContents(12,85);s.Rows().Style.Alignment.WrapText=true;s.Rows().Style.Alignment.Vertical=XLAlignmentVerticalValues.Top;}
}
