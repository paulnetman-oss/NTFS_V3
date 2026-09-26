using ClosedXML.Excel;
namespace NTFSSuite;
public static class TemplateService
{
 public static List<DirectoryItem> ReadDirectories(string template,string root,int levels)
 {
  using var wb=new XLWorkbook(template);var ws=wb.Worksheet("PRINCIPAL");var result=new List<DirectoryItem>();string[] current=new string[levels];
  for(int row=4;row<=ws.LastRowUsed().RowNumber();row++){string[] values=Enumerable.Range(1,levels).Select(c=>ws.Cell(row,c).GetString().Trim()).ToArray();if(values.All(string.IsNullOrWhiteSpace))break;if(values.Any(x=>x.StartsWith("NIVEL ",StringComparison.OrdinalIgnoreCase)))break;bool changed=false;for(int i=0;i<levels;i++){if(values[i].Length==0)continue;current[i]=values[i];Array.Clear(current,i+1,levels-i-1);changed=true;}if(!changed)continue;string[] parts=current.Where(x=>!string.IsNullOrWhiteSpace(x)).ToArray();string? full=PathResolver.Resolve(root,parts,out string status);result.Add(new DirectoryItem{Row=row,RelativePath=string.Join("\\",parts),FullPath=full??"",Levels=(string[])current.Clone(),Resolution=status});}
  return result;
 }
 public static bool IsFluorescentYellow(IXLCell cell){var x=cell.Style.Fill.BackgroundColor;if(x.ColorType!=XLColorType.Color)return false;var c=x.Color;return c.R==255&&c.G==255&&c.B==0;}
}