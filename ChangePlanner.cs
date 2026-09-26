using ClosedXML.Excel;
namespace NTFSSuite;
public static class ChangePlanner
{
 public static void Run(string matrix,string root,string output,int levels)
 {
  using var wb=new XLWorkbook(matrix);var ws=wb.Worksheet("PRINCIPAL");var dirs=TemplateService.ReadDirectories(matrix,root,levels).ToDictionary(x=>x.Row);var items=new List<ChangePlanItem>();
  for(int r=4;r<=ws.LastRowUsed().RowNumber();r++){if(!dirs.TryGetValue(r,out var d))continue;for(int c=levels+1;c<=ws.LastColumnUsed().ColumnNumber();c++){var cell=ws.Cell(r,c);if(!TemplateService.IsFluorescentYellow(cell))continue;string requested=cell.GetString().Trim().ToUpperInvariant();string status=new[]{"L","M","LEM","N","E","S/A"}.Contains(requested)?"LISTO PARA REVISIÓN":"VALOR NO VÁLIDO";items.Add(new ChangePlanItem{Cell=cell.Address.ToString(),Path=d.RelativePath,FriendlyName=ws.Cell(3,c).GetString(),DisplayName=ws.Cell(2,c).GetString(),Requested=requested,Status=status,Notes=d.FullPath.Length==0?"Ruta no resuelta":"Simulación: no se modifica NTFS"});}}
  using var outWb=new XLWorkbook();var p=outWb.AddWorksheet("PLAN");string[] h={"Celda","Ruta","Friendly Name","DisplayName","Solicitado","Estado","Notas"};for(int i=0;i<h.Length;i++){p.Cell(1,i+1).Value=h[i];p.Cell(1,i+1).Style.Font.Bold=true;}int row=2;foreach(var x in items){object[] v={x.Cell,x.Path,x.FriendlyName,x.DisplayName,x.Requested,x.Status,x.Notes};for(int i=0;i<v.Length;i++)p.Cell(row,i+1).Value=XLCellValue.FromObject(v[i]);row++;}p.Columns().AdjustToContents(12,80);outWb.SaveAs(output);
 }
}