using ClosedXML.Excel;
using System.Security.AccessControl;
using System.Security.Principal;
namespace NTFSSuite;
public static class AclPatternAnalyzer
{
 public static void Run(string template,string root,int levels,string output,Action<int,string>? progress=null)
 {
  var dirs=TemplateService.ReadDirectories(template,root,levels);var patterns=new Dictionary<string,AclPattern>(StringComparer.Ordinal);var examples=new List<AclExample>();var resolver=new PrincipalResolver();int done=0,errors=0;
  foreach(var d in dirs){done++;progress?.Invoke(done*80/Math.Max(1,dirs.Count),d.RelativePath);if(string.IsNullOrWhiteSpace(d.FullPath)||!Directory.Exists(d.FullPath))continue;DirectorySecurity security;try{security=FileSystemAclExtensions.GetAccessControl(new DirectoryInfo(d.FullPath),AccessControlSections.Access);}catch{errors++;continue;}
   foreach(FileSystemAccessRule rule in security.GetAccessRules(true,true,typeof(SecurityIdentifier))){if(rule.IdentityReference is not SecurityIdentifier sid)continue;var principal=resolver.Resolve(sid);string scope=Scope(rule),rights=Rights(rule.FileSystemRights),suggested=Suggest(rule);string signature=$"{rule.AccessControlType}|{scope}|{rights}|Inherited={rule.IsInherited}";if(!patterns.TryGetValue(signature,out var pattern)){pattern=new AclPattern{Signature=signature,Type=rule.AccessControlType.ToString(),Scope=scope,Rights=rights,Suggested=suggested};patterns.Add(signature,pattern);}pattern.Frequency++;examples.Add(new AclExample{Signature=signature,AccountName=principal.AccountName,DisplayName=principal.DisplayName,Sid=principal.Sid,PrincipalType=principal.PrincipalType,ResolutionMethod=principal.ResolutionMethod,IsResolved=principal.IsResolved,Path=d.RelativePath,AccessType=rule.AccessControlType.ToString(),Scope=scope,Rights=rights,Inherited=rule.IsInherited,Suggested=suggested});}
  }
  int n=1;foreach(var p in patterns.Values.OrderByDescending(x=>x.Frequency).ThenBy(x=>x.Signature))p.Id="ACL"+(n++).ToString("000");var ids=patterns.Values.ToDictionary(x=>x.Signature,x=>x.Id);foreach(var e in examples)e.PatternId=ids[e.Signature];SelectExamples(patterns.Values,examples);progress?.Invoke(90,"Generando reporte");Write(output,patterns.Values.OrderBy(x=>x.Id).ToList(),examples,dirs,errors);progress?.Invoke(100,"Finalizado");
 }
 static void SelectExamples(IEnumerable<AclPattern> patterns,List<AclExample> all)
 {
  var groups=all.GroupBy(x=>x.Signature).ToDictionary(g=>g.Key,g=>g.ToList());
  foreach(var p in patterns){var list=groups[p.Signature];var distinct=list.GroupBy(x=>x.Sid,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).ToList();p.DistinctPrincipals=distinct.Count;p.ResolvedPrincipals=distinct.Count(x=>x.IsResolved);p.UnresolvedPrincipals=distinct.Count(x=>!x.IsResolved);p.Example=list.OrderBy(x=>x.IsResolved?0:1).ThenBy(x=>Priority(x.PrincipalType)).ThenBy(x=>x.DisplayName,StringComparer.CurrentCultureIgnoreCase).First();}
 }
 static int Priority(string t)=>t switch{"Usuario"=>0,"Grupo"=>1,"Cuenta"=>2,"Grupo integrado"=>3,"Identidad integrada"=>4,"Equipo"=>5,_=>6};
 static string Rights(FileSystemRights r)=>(r&~FileSystemRights.Synchronize).ToString();
 static string Scope(FileSystemAccessRule r)=>r.InheritanceFlags==(InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit)&&r.PropagationFlags==PropagationFlags.None?"Esta carpeta, subcarpetas y archivos":r.InheritanceFlags==InheritanceFlags.None?"Solo esta carpeta":r.InheritanceFlags+" / "+r.PropagationFlags;
 static string Suggest(FileSystemAccessRule r){var n=r.FileSystemRights&~FileSystemRights.Synchronize;if(r.AccessControlType==AccessControlType.Allow&&n==FileSystemRights.FullControl)return "LEM";if(r.AccessControlType==AccessControlType.Allow&&(n&FileSystemRights.Modify)==FileSystemRights.Modify)return "M";if(r.AccessControlType==AccessControlType.Allow&&(n&FileSystemRights.ReadAndExecute)==FileSystemRights.ReadAndExecute)return "L";return "SIN CLASIFICAR";}
 static void Write(string output,List<AclPattern> patterns,List<AclExample> examples,List<DirectoryItem> dirs,int errors)
 {
  using var wb=new XLWorkbook();var s=wb.AddWorksheet("RESUMEN");Header(s,new[]{"Concepto","Cantidad"});object[,] summary={{"Directorios documentados",dirs.Count},{"Directorios resueltos",dirs.Count(x=>x.FullPath.Length>0)},{"Combinaciones únicas",patterns.Count},{"Entradas ACL analizadas",examples.Count},{"Entradas resueltas",examples.Count(x=>x.IsResolved)},{"Entradas SID no resueltas",examples.Count(x=>!x.IsResolved)},{"Errores ACL",errors}};for(int i=0;i<summary.GetLength(0);i++){s.Cell(i+2,1).Value=XLCellValue.FromObject(summary[i,0]);s.Cell(i+2,2).Value=XLCellValue.FromObject(summary[i,1]);}s.Columns().AdjustToContents();
  WritePatterns(wb,"COMBINACIONES DETECTADAS",patterns);WriteExamples(wb,examples);WritePatterns(wb,"SIN CLASIFICAR",patterns.Where(x=>x.Suggested=="SIN CLASIFICAR"));WriteUnresolved(wb,examples);var r=wb.AddWorksheet("RUTAS");Header(r,new[]{"Fila","Ruta documentada","Ruta física","Estado"});int row=2;foreach(var d in dirs)Put(r,row++,new object[]{d.Row,d.RelativePath,d.FullPath,d.Resolution});Finish(r,4);wb.SaveAs(output);
 }
 static void WritePatterns(XLWorkbook wb,string name,IEnumerable<AclPattern> patterns)
 {
  var s=wb.AddWorksheet(name);Header(s,new[]{"ID","Frecuencia","Principales distintos","Principales resueltos","SID no resueltos","Permitir/Denegar","Se aplica a","Heredado ejemplo","Derechos avanzados","Clasificación sugerida","Estado del ejemplo","Cuenta ejemplo","DisplayName ejemplo","Tipo principal","Ruta ejemplo","SID técnico","Método técnico"});int row=2;foreach(var p in patterns){var e=p.Example!;Put(s,row,new object[]{p.Id,p.Frequency,p.DistinctPrincipals,p.ResolvedPrincipals,p.UnresolvedPrincipals,p.Type,p.Scope,e.Inherited?"Sí":"No",p.Rights,p.Suggested,e.IsResolved?"RESUELTO":"NO RESUELTO",e.IsResolved?e.AccountName:"Sin cuenta vigente",e.IsResolved?e.DisplayName:"SID NO RESUELTO",e.PrincipalType,e.Path,e.Sid,e.ResolutionMethod});if(!e.IsResolved)s.Row(row).Style.Fill.BackgroundColor=XLColor.FromHtml("#F4CCCC");row++;}Finish(s,17);
 }
 static void WriteExamples(XLWorkbook wb,IEnumerable<AclExample> examples)
 {
  var s=wb.AddWorksheet("EJEMPLOS");Header(s,new[]{"ID","Estado","Cuenta","DisplayName","Tipo principal","Ruta","Permitir/Denegar","Se aplica a","Derechos avanzados","Heredado","Clasificación sugerida","SID técnico","Método técnico"});int row=2;foreach(var e in examples.OrderBy(x=>x.PatternId).ThenBy(x=>x.IsResolved?0:1).ThenBy(x=>x.DisplayName)){Put(s,row++,new object[]{e.PatternId,e.IsResolved?"RESUELTO":"NO RESUELTO",e.IsResolved?e.AccountName:"Sin cuenta vigente",e.IsResolved?e.DisplayName:"SID NO RESUELTO",e.PrincipalType,e.Path,e.AccessType,e.Scope,e.Rights,e.Inherited?"Sí":"No",e.Suggested,e.Sid,e.ResolutionMethod});}Finish(s,13);
 }
 static void WriteUnresolved(XLWorkbook wb,IEnumerable<AclExample> examples){var s=wb.AddWorksheet("SID NO RESUELTOS");Header(s,new[]{"ID","SID","Ruta","Permitir/Denegar","Se aplica a","Derechos","Heredado"});int row=2;foreach(var e in examples.Where(x=>!x.IsResolved))Put(s,row++,new object[]{e.PatternId,e.Sid,e.Path,e.AccessType,e.Scope,e.Rights,e.Inherited?"Sí":"No"});Finish(s,7);}
 static void Header(IXLWorksheet s,string[] h){for(int i=0;i<h.Length;i++){var c=s.Cell(1,i+1);c.Value=h[i];c.Style.Font.Bold=true;c.Style.Font.FontColor=XLColor.White;c.Style.Fill.BackgroundColor=XLColor.FromHtml("#1F4E78");}}
 static void Put(IXLWorksheet s,int row,object[] v){for(int i=0;i<v.Length;i++)s.Cell(row,i+1).Value=XLCellValue.FromObject(v[i]);}
 static void Finish(IXLWorksheet s,int cols){s.SheetView.FreezeRows(1);if(s.LastRowUsed()!=null)s.Range(1,1,s.LastRowUsed().RowNumber(),cols).SetAutoFilter();s.Columns(1,cols).AdjustToContents(12,80);}
}
