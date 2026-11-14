using System.ComponentModel.DataAnnotations;
using COSGeniusSystem.Exceptions;
namespace COSGeniusSystem.Commons;
public sealed class PatientPhoneAttribute : ValidationAttribute {
 public override bool IsValid(object? value) {
  try { PatientPhone.Validate(value as string); return true; }
  catch (BadRequestException) { return false; }
 }
 public PatientPhoneAttribute() { ErrorMessage = "Số điện thoại không đúng định dạng."; }
}
