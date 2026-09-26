using System.Globalization;using System.Text;using System.Text.RegularExpressions;
namespace NTFSSuite;
public static class TextNormalizer
{
 public static string Key(string? value){if(string.IsNullOrWhiteSpace(value))return "";string s=Regex.Replace(value.Trim(),@"\s+"," ").Normalize(NormalizationForm.FormD);var b=new StringBuilder();foreach(char c in s)if(CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark)b.Append(char.ToUpperInvariant(c));return b.ToString().Normalize(NormalizationForm.FormC);}
}