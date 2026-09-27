using System.DirectoryServices;
using System.Security.Principal;
namespace NTFSSuite;
public sealed class PrincipalResolver
{
 private readonly Dictionary<string,PrincipalInfo> cache=new(StringComparer.OrdinalIgnoreCase);
 public PrincipalInfo Resolve(SecurityIdentifier sid)
 {
  if(cache.TryGetValue(sid.Value,out var old))return old;
  var p=new PrincipalInfo{Sid=sid.Value,AccountName=sid.Value,DisplayName="SID NO RESUELTO",PrincipalType="Desconocido",ResolutionMethod="SID no traducido"};
  try{string a=((NTAccount)sid.Translate(typeof(NTAccount))).Value;if(!a.StartsWith("S-1-",StringComparison.OrdinalIgnoreCase)){p.AccountName=a;p.DisplayName=Leaf(a);p.PrincipalType=Infer(a);p.IsResolved=true;p.ResolutionMethod="Traducción SID a cuenta";}}catch{}
  try{using var e=new DirectoryEntry("LDAP://<SID="+sid.Value+">");e.RefreshCache(new[]{"displayName","name","sAMAccountName","objectClass"});string display=Get(e,"displayName");if(string.IsNullOrWhiteSpace(display))display=Get(e,"name");string sam=Get(e,"sAMAccountName");if(!string.IsNullOrWhiteSpace(sam)){string dom=Domain(p.AccountName);p.AccountName=string.IsNullOrWhiteSpace(dom)?sam:dom+"\\"+sam;}p.DisplayName=display;p.PrincipalType=Type(e);p.IsResolved=true;p.ResolutionMethod="Active Directory por SID";}catch{}
  if(!p.IsResolved){p.AccountName=sid.Value;p.DisplayName="SID NO RESUELTO";p.PrincipalType="Desconocido";}
  cache[sid.Value]=p;return p;
 }
 static string Get(DirectoryEntry e,string n)=>Convert.ToString(e.Properties[n].Value)??"";
 static string Type(DirectoryEntry e){foreach(object x in e.Properties["objectClass"]){string s=Convert.ToString(x)??"";if(s.Equals("group",StringComparison.OrdinalIgnoreCase))return "Grupo";if(s.Equals("computer",StringComparison.OrdinalIgnoreCase))return "Equipo";if(s.Equals("user",StringComparison.OrdinalIgnoreCase))return "Usuario";}return "Cuenta";}
 static string Infer(string a)=>a.StartsWith("BUILTIN\\",StringComparison.OrdinalIgnoreCase)?"Grupo integrado":a.StartsWith("NT AUTHORITY\\",StringComparison.OrdinalIgnoreCase)?"Identidad integrada":a.EndsWith("$")?"Equipo":"Cuenta";
 static string Leaf(string a){int i=a.LastIndexOf('\\');return i>=0?a[(i+1)..]:a;}
 static string Domain(string a){int i=a.IndexOf('\\');return i>0?a[..i]:"";}
}
