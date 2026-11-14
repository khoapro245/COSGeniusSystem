using System.Text.RegularExpressions;
using COSGeniusSystem.Exceptions;
namespace COSGeniusSystem.Commons;
public static class PatientPhone {
 public static string? Normalize(string? raw) {
  if (string.IsNullOrWhiteSpace(raw)) return null;
  var value = Regex.Replace(raw, @"[\s.()\-]", "");
  return value.StartsWith("+84") ? "0" + value[3..] : value;
 }
 public static string? Validate(string? raw, bool required = false, bool contact = false) {
  var value = Normalize(raw);
  if (value == null && !required) return null;
  if (value == null || !Regex.IsMatch(value, contact ? @"^0[0-9]{9,10}$" : @"^0[0-9]{9}$"))
   throw new BadRequestException("Số điện thoại không đúng định dạng.");
  return value;
 }
}
