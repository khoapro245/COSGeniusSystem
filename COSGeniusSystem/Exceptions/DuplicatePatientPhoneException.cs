using COSGeniusSystem.DTOs;
namespace COSGeniusSystem.Exceptions;
public sealed class DuplicatePatientPhoneException(List<PatientResponse> patients) : ApiException(409,
 "This phone number is already used by another patient. Please review before continuing.") {
 public object Warning => new { warningCode = "PATIENT_PHONE_DUPLICATE", patients };
}
