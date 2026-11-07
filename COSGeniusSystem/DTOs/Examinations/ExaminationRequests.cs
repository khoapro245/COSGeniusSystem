using System.ComponentModel.DataAnnotations;

namespace COSGeniusSystem.DTOs;

public class PageQuery
{
    [Range(1, 1000000)] public int PageNumber { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 10;
    [StringLength(150)] public string? Search { get; set; }
}

public class RegistrationQuery : PageQuery
{
    [Range(1, int.MaxValue)] public int? PatientId { get; set; }
    public string? Session { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    [Range(1, int.MaxValue)] public int? DoctorId { get; set; }
    public string? Status { get; set; }
    public string? Keyword { get; set; }
    public string? PatientKeyword { get; set; }
    public string? Phone { get; set; }
    public string? Code { get; set; }
    public string? VisitCode { get; set; }
    public string? PatientName { get; set; }
    public string? PatientCode { get; set; }
    public string? RegistrationCode { get; set; }
}

public class ExaminationFilterDto : RegistrationQuery { }

public sealed class DoctorWorkloadQuery
{
    [Required] public DateOnly? BusinessDate { get; set; }
    [Required, RegularExpression("^(MORNING|AFTERNOON)$")] public string Session { get; set; } = "";
}

public class RegistrationInput
{
    public DateOnly? BusinessDate { get; set; }
    [Required, RegularExpression("^(MORNING|AFTERNOON)$")] public string Session { get; set; } = "";
    [Range(1, int.MaxValue)] public int DoctorId { get; set; }
    [Range(1, int.MaxValue)] public int? ContactId { get; set; }
    public bool IsPriority { get; set; }
    [StringLength(10000)] public string? Symptoms { get; set; }
    [StringLength(10000)] public string? Note { get; set; }
    public string? DoctorAdvice { get; set; }
}

public class CreateRegistrationRequest : RegistrationInput
{
    [Range(1, int.MaxValue)] public int PatientId { get; set; }
}

public class CreateExaminationRegistrationDto : CreateRegistrationRequest { }

public sealed class UpdateRegistrationRequest : RegistrationInput
{
    [Required, MinLength(8), MaxLength(8)] public byte[] RowVersion { get; set; } = [];
}

public class CancelTicketRequest
{
    [Required(ErrorMessage = "Lý do hủy là bắt buộc (CancelReason is required).")]
    public string CancelReason { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public byte[]? RowVersion { get; set; }
}

public class VersionRequest
{
    [Required, MinLength(8), MaxLength(8)] public byte[] RowVersion { get; set; } = [];
}

public class MedicalRecordData
{
    [StringLength(10000, ErrorMessage = "Triệu chứng không được vượt quá 10.000 ký tự.")] public string? Symptoms { get; set; }
    [StringLength(500, ErrorMessage = "Chẩn đoán sơ bộ không được vượt quá 500 ký tự.")] public string? PreliminaryDiagnosis { get; set; }
    [StringLength(500, ErrorMessage = "Chẩn đoán xác định không được vượt quá 500 ký tự.")] public string? FinalDiagnosis { get; set; }
    [StringLength(10000, ErrorMessage = "Lời dặn của bác sĩ không được vượt quá 10.000 ký tự.")] public string? DoctorAdvice { get; set; }
    [Range(1, 400, ErrorMessage = "Mạch phải là số nguyên từ 1 đến 400 lần/phút.")] public short? PulseRate { get; set; }
    [Range(typeof(decimal), "20", "50", ErrorMessage = "Nhiệt độ phải là số từ 20 đến 50 °C.")] public decimal? Temperature { get; set; }
    [Range(1, 150, ErrorMessage = "Nhịp thở phải là số nguyên từ 1 đến 150 lần/phút.")] public short? RespiratoryRate { get; set; }
    [Range(typeof(decimal), "1", "300", ErrorMessage = "Chiều cao phải là số từ 1 đến 300 cm.")] public decimal? HeightCm { get; set; }
    [Range(typeof(decimal), "0.1", "1000", ErrorMessage = "Cân nặng phải là số từ 0.1 đến 1000 kg.")] public decimal? WeightKg { get; set; }
    [Range(typeof(decimal), "0", "100", ErrorMessage = "SpO2 phải là số từ 0 đến 100%.")] public decimal? SpO2 { get; set; }
    [StringLength(10000, ErrorMessage = "Vọng chẩn không được vượt quá 10.000 ký tự.")] public string? Inspection { get; set; }
    [StringLength(10000, ErrorMessage = "Văn chẩn không được vượt quá 10.000 ký tự.")] public string? ListeningSmelling { get; set; }
    [StringLength(10000, ErrorMessage = "Vấn chẩn không được vượt quá 10.000 ký tự.")] public string? Inquiry { get; set; }
    [StringLength(10000, ErrorMessage = "Thiết chẩn không được vượt quá 10.000 ký tự.")] public string? Palpation { get; set; }
    [StringLength(10000, ErrorMessage = "Tóm tắt tứ chẩn không được vượt quá 10.000 ký tự.")] public string? FourExaminationSummary { get; set; }
    [StringLength(10000, ErrorMessage = "Biện chứng không được vượt quá 10.000 ký tự.")] public string? SyndromeDifferentiation { get; set; }
    [StringLength(10000, ErrorMessage = "Chẩn đoán YHCT không được vượt quá 10.000 ký tự.")] public string? TraditionalDiagnosis { get; set; }
    [StringLength(20, ErrorMessage = "Mã ICD không được vượt quá 20 ký tự.")] public string? Icdcode { get; set; }
    [StringLength(500, ErrorMessage = "Diễn giải ICD không được vượt quá 500 ký tự.")] public string? Icddescription { get; set; }
}

public sealed class SaveMedicalRecordRequest : MedicalRecordData
{
    [Required, MinLength(8), MaxLength(8)] public byte[] RowVersion { get; set; } = [];
}

public sealed class PrescriptionItemRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Mã vị thuốc không hợp lệ.")] public int HerbId { get; set; }
    [Range(typeof(decimal), "0.001", "999999.999", ErrorMessage = "Liều dùng mỗi thang phải là số lớn hơn 0.")] public decimal DosagePerDose { get; set; }
    [StringLength(50, ErrorMessage = "Vai trò trong đơn không quá 50 ký tự.")] public string? RoleInFormula { get; set; }
    [StringLength(255, ErrorMessage = "Ghi chú vị thuốc không quá 255 ký tự.")] public string? Note { get; set; }
}

public sealed class SavePrescriptionRequest : VersionRequest
{
    [StringLength(150, ErrorMessage = "Tên đơn thuốc không quá 150 ký tự.")] public string? PrescriptionName { get; set; }
    [Range(1, 10000, ErrorMessage = "Số thang phải là số nguyên từ 1 đến 10.000.")] public int TotalDoses { get; set; } = 1;
    [Range(1, 10000, ErrorMessage = "Số ngày dùng phải là số nguyên từ 1 đến 10.000.")] public int? DosageDays { get; set; }
    [StringLength(10000, ErrorMessage = "Hướng dẫn sử dụng không quá 10.000 ký tự.")] public string? UsageInstructions { get; set; }
    [StringLength(10000, ErrorMessage = "Ghi chú không quá 10.000 ký tự.")] public string? Note { get; set; }
    [Required(ErrorMessage = "Danh sách vị thuốc không được để trống."), MaxLength(500)] public List<PrescriptionItemRequest> Items { get; set; } = [];
}

public sealed class LoadFormulaRequest : VersionRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Mã công thức không hợp lệ.")] public int HerbalFormulaId { get; set; }
}

public sealed class PlanItemRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Mã dịch vụ không hợp lệ.")] public int ServiceId { get; set; }
    [Range(1, 10000, ErrorMessage = "Số lượng dịch vụ phải là số nguyên từ 1 đến 10.000.")] public int PlannedQuantity { get; set; } = 1;
    [StringLength(10000, ErrorMessage = "Hướng dẫn không quá 10.000 ký tự.")] public string? Instructions { get; set; }
}

public sealed class SaveTreatmentPlanRequest : VersionRequest
{
    [Required(ErrorMessage = "Tên chỉ định / Liệu trình không được để trống."), StringLength(200, ErrorMessage = "Tên chỉ định không quá 200 ký tự.")] public string PlanName { get; set; } = "";
    [Required, RegularExpression("^(SINGLE|COURSE)$", ErrorMessage = "Hình thức điều trị chỉ chấp nhận SINGLE hoặc COURSE.")] public string TreatmentType { get; set; } = "SINGLE";
    [Range(1, 10000, ErrorMessage = "Tổng số buổi phải là số nguyên từ 1 đến 10.000.")] public int PlannedSessionCount { get; set; } = 1;
    [StringLength(10000, ErrorMessage = "Mô tả không quá 10.000 ký tự.")] public string? Description { get; set; }
    [Required(ErrorMessage = "Danh sách dịch vụ không được để trống."), MaxLength(500)] public List<PlanItemRequest> Items { get; set; } = [];
    [MaxLength(10000)] public List<PlannedSessionRequest>? Sessions { get; set; }
}

public sealed class PlannedSessionRequest
{
    [Range(1, 10000, ErrorMessage = "Số thứ tự buổi phải là số nguyên từ 1 đến 10.000.")] public int SessionNumber { get; set; }
    [Required(ErrorMessage = "Danh sách dịch vụ trong buổi không được để trống."), MaxLength(500)] public List<int> ServiceIds { get; set; } = [];
}
