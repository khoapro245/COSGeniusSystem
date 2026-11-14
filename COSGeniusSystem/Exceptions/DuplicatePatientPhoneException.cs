using COSGeniusSystem.DTOs;
namespace COSGeniusSystem.Exceptions;
public sealed class DuplicatePatientPhoneException(List<PatientResponse> patients) : ApiException(409,
 "Số điện thoại này đã được sử dụng trong hồ sơ bệnh nhân khác. Vui lòng kiểm tra trước khi tiếp tục.") {
 public object Warning => new { warningCode = "PATIENT_PHONE_DUPLICATE", patients };
}
