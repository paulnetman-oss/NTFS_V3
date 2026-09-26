namespace NTFSSuite;
public static class PathResolver
{
 public static string? Resolve(string root,IEnumerable<string> documented,out string status)
 {
  string current=Path.GetFullPath(root.TrimEnd(Path.DirectorySeparatorChar));var parts=documented.Where(x=>!string.IsNullOrWhiteSpace(x)).Select(x=>x.Trim()).ToList();string rootName=new DirectoryInfo(current).Name;if(parts.Count>0&&TextNormalizer.Key(parts[0])==TextNormalizer.Key(rootName))parts.RemoveAt(0);
  foreach(string part in parts){string exact=Path.Combine(current,part);if(Directory.Exists(exact)){current=exact;continue;}List<string> hits;try{hits=Directory.EnumerateDirectories(current).Where(x=>TextNormalizer.Key(Path.GetFileName(x))==TextNormalizer.Key(part)).ToList();}catch(Exception e){status="ERROR: "+e.Message;return null;}if(hits.Count==0){status="NO ENCONTRADO: "+part;return null;}if(hits.Count>1){status="AMBIGUO: "+part;return null;}current=hits[0];}
  status="OK";return current;
 }
}