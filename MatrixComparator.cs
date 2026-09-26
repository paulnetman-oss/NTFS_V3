using ClosedXML.Excel;using System.Globalization;using System.Text;
namespace NTFSSuite;
public static class MatrixComparator
{
 public static void Run(string client,string scan,string output,int levels)
 {
  using var cw=new XLWorkbook(client);using var sw=new XLWorkbook(scan);var c=cw.Worksheet("PRINCIPAL");var s=sw.Worksheet("PRINCIPAL");int first=levels+1;var cp=ReadPaths(c,levels);var sp=ReadPaths(s,levels).ToDictionary(x=>x.Key,x=>x.Row);var users=Math.Min(c.LastColumnUsed().ColumnNumber(),s.LastColumnUsed().ColumnNumber())-first+1;
  using var outWb=new XLWorkbook();var o=outWb.AddWorksheet("COMPARACIÓN");int last=levels+users*2;o.Range(1,1,1,last).Merge();o.Cell(1,1).Value="CLIENTE VS ESCANEO";o.Cell(1,1).Style.Fill.BackgroundColor=XLColor.FromHtml("#1F4E78");o.Cell(1,1).Style.Font.FontColor=XLColor.White;o.Cell(1,1).Style.Font.Bold=true;
  for(int i=1;i<=levels;i++){o.Cell(2,i).Value="NIVEL "+i;o.Cell(2,i).Style.Fill.BackgroundColor=XLColor.FromHtml("#1F4E78");o.Cell(2,i).Style.Font.FontColor=XLColor.White;}
  for(int u=0;u<users;u++){int a=first+u*2,b=a+1,source=first+u;o.Cell(2,a).Value=c.Cell(3,source).GetString();o.Cell(3,a).Value="Cliente";o.Cell(3,b).Value="Escaneo";o.Cell(3,a).Style.Fill.BackgroundColor=XLColor.FromHtml("#D9EAD3");o.Cell(3,b).Style.Fill.BackgroundColor=XLColor.FromHtml("#D9EAF7");}
  int row=4;foreach(var p in cp){if(!sp.TryGetValue(p.Key,out int sr))continue;for(int i=0;i<levels;i++)o.Cell(row,i+1).Value=p.Levels[i];for(int u=0;u<users;u++){int a=first+u*2,b=a+1,src=first+u;string cv=c.Cell(p.Row,src).GetString(),sv=s.Cell(sr,src).GetString();o.Cell(row,a).Value=cv;o.Cell(row,b).Value=sv;o.Cell(row,a).Style.Fill.BackgroundColor=XLColor.FromHtml("#D9EAD3");o.Cell(row,b).Style.Fill.BackgroundColor=XLColor.FromHtml("#D9EAF7");if(Key(cv)!=Key(sv)){o.Cell(row,a).Style.Fill.BackgroundColor=XLColor.Yellow;o.Cell(row,b).Style.Fill.BackgroundColor=XLColor.Yellow;}}row++;}
  o.SheetView.Freeze(3,levels);o.Columns(1,levels).Width=22;o.Columns(first,last).Width=5;outWb.SaveAs(output);
 }
 sealed record P(int Row,string Key,string[] Levels);
 static List<P> ReadPaths(IXLWorksheet w,int levels){var list=new List<P>();string[] cur=new string[levels];for(int r=4;r<=w.LastRowUsed().RowNumber();r++){var v=Enumerable.Range(1,levels).Select(c=>w.Cell(r,c).GetString().Trim()).ToArray();if(v.All(string.IsNullOrWhiteSpace))break;bool ch=false;for(int i=0;i<levels;i++){if(v[i].Length==0)continue;cur[i]=v[i];Array.Clear(cur,i+1,levels-i-1);ch=true;}if(ch)list.Add(new P(r,string.Join("\\",cur.Where(x=>x.Length>0).Select(Key)),(string[])cur.Clone()));}return list;}
 static string Key(string? x)=>TextNormalizer.Key(x);
}