using System.ComponentModel.DataAnnotations;

namespace COSGeniusSystem.DTOs;

public class PageQuery
{
    [Range(1, 1000000, ErrorMessage = "The value is outside the allowed range.")] public int PageNumber { get; set; } = 1;
    [Range(1, 100, ErrorMessage = "The value is outside the allowed range.")] public int PageSize { get; set; } = 10;
    [StringLength(150, ErrorMessage = "Invalid data length.")] public string? Search { get; set; }
}

public class RegistrationQuery : PageQuery
{
    [Range(1, int.MaxValue, ErrorMessage = "The value is outside the allowed range.")] public int? PatientId { get; set; }
    public string? Session { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "The value is outside the allowed range.")] public int? DoctorId { get; set; }
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
    [Required(ErrorMessage = "This field is required.")] public DateOnly? BusinessDate { get; set; }
    [Required(ErrorMessage = "This field is required."), RegularExpression("^(MORNING|AFTERNOON)$")] public string Session { get; set; } = "";
}

public class RegistrationInput
{
    public DateOnly? BusinessDate { get; set; }
    [Required(ErrorMessage = "This field is required."), RegularExpression("^(MORNING|AFTERNOON)$")] public string Session { get; set; } = "";
    [Range(1, int.MaxValue, ErrorMessage = "The value is outside the allowed range.")] public int DoctorId { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "The value is outside the allowed range.")] public int? ContactId { get; set; }
    public bool IsPriority { get; set; }
    [StringLength(10000, ErrorMessage = "Invalid data length.")] public string? Symptoms { get; set; }
    [StringLength(10000, ErrorMessage = "Invalid data length.")] public string? Note { get; set; }
    public string? DoctorAdvice { get; set; }
}

public class CreateRegistrationRequest : RegistrationInput
{
    [Range(1, int.MaxValue, ErrorMessage = "The value is outside the allowed range.")] public int PatientId { get; set; }
}

public class CreateExaminationRegistrationDto : CreateRegistrationRequest { }

public sealed class UpdateRegistrationRequest : RegistrationInput
{
    [Required(ErrorMessage = "This field is required."), MinLength(8, ErrorMessage = "The value is shorter than the minimum allowed length."), MaxLength(8, ErrorMessage = "The value exceeds the maximum allowed length.")] public byte[] RowVersion { get; set; } = [];
}

public class CancelTicketRequest
{
    private string _cancelReason = string.Empty;

    [Required(ErrorMessage = "A cancellation reason is required.")]
    public string CancelReason
    {
        get => string.IsNullOrWhiteSpace(_cancelReason) ? (Reason ?? string.Empty) : _cancelReason;
        set => _cancelReason = value;
    }

    public string? Reason { get; set; }
    public byte[]? RowVersion { get; set; }
}

public class VersionRequest
{
    [Required(ErrorMessage = "This field is required."), MinLength(8, ErrorMessage = "The value is shorter than the minimum allowed length."), MaxLength(8, ErrorMessage = "The value exceeds the maximum allowed length.")] public byte[] RowVersion { get; set; } = [];
}

public class MedicalRecordData
{
    [StringLength(10000, ErrorMessage = "Symptoms must not exceed 10,000 characters.")] public string? Symptoms { get; set; }
    [StringLength(500, ErrorMessage = "Preliminary diagnosis must not exceed 500 characters.")] public string? PreliminaryDiagnosis { get; set; }
    [StringLength(500, ErrorMessage = "Final diagnosis must not exceed 500 characters.")] public string? FinalDiagnosis { get; set; }
    [StringLength(10000, ErrorMessage = "Doctor instructions must not exceed 10,000 characters.")] public string? DoctorAdvice { get; set; }
    [Range(1, 400, ErrorMessage = "Pulse must be an integer between 1 and 400 beats per minute.")] public short? PulseRate { get; set; }
    [Range(typeof(decimal), "20", "50", ErrorMessage = "Temperature must be a number between 20 and 50 degrees Celsius.")] public decimal? Temperature { get; set; }
    [Range(1, 150, ErrorMessage = "Respiratory rate must be an integer between 1 and 150 breaths per minute.")] public short? RespiratoryRate { get; set; }
    [Range(typeof(decimal), "1", "300", ErrorMessage = "Height must be a number between 1 and 300 cm.")] public decimal? HeightCm { get; set; }
    [Range(typeof(decimal), "0.1", "1000", ErrorMessage = "Weight must be a number between 0.1 and 1000 kg.")] public decimal? WeightKg { get; set; }
    [Range(typeof(decimal), "0", "100", ErrorMessage = "SpO2 must be a number between 0 and 100 percent.")] public decimal? SpO2 { get; set; }
    [StringLength(10000, ErrorMessage = "Inspection findings must not exceed 10,000 characters.")] public string? Inspection { get; set; }
    [StringLength(10000, ErrorMessage = "Listening and smelling findings must not exceed 10,000 characters.")] public string? ListeningSmelling { get; set; }
    [StringLength(10000, ErrorMessage = "Inquiry findings must not exceed 10,000 characters.")] public string? Inquiry { get; set; }
    [StringLength(10000, ErrorMessage = "Palpation findings must not exceed 10,000 characters.")] public string? Palpation { get; set; }
    [StringLength(10000, ErrorMessage = "The four-examination summary must not exceed 10,000 characters.")] public string? FourExaminationSummary { get; set; }
    [StringLength(10000, ErrorMessage = "Syndrome differentiation must not exceed 10,000 characters.")] public string? SyndromeDifferentiation { get; set; }
    [StringLength(10000, ErrorMessage = "Traditional medicine diagnosis must not exceed 10,000 characters.")] public string? TraditionalDiagnosis { get; set; }
    [StringLength(20, ErrorMessage = "ICD code must not exceed 20 characters.")] public string? Icdcode { get; set; }
    [StringLength(500, ErrorMessage = "ICD description must not exceed 500 characters.")] public string? Icddescription { get; set; }
}

public sealed class SaveMedicalRecordRequest : MedicalRecordData
{
    [Required(ErrorMessage = "This field is required."), MinLength(8, ErrorMessage = "The value is shorter than the minimum allowed length."), MaxLength(8, ErrorMessage = "The value exceeds the maximum allowed length.")] public byte[] RowVersion { get; set; } = [];
}

public sealed class PrescriptionItemRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Invalid herb identifier.")] public int HerbId { get; set; }
    [Range(typeof(decimal), "0.001", "999999.999", ErrorMessage = "The dose per pack must be greater than 0.")] public decimal DosagePerDose { get; set; }
    [StringLength(50, ErrorMessage = "Prescription role must not exceed 50 characters.")] public string? RoleInFormula { get; set; }
    [StringLength(255, ErrorMessage = "Herb notes must not exceed 255 characters.")] public string? Note { get; set; }
}

public sealed class SavePrescriptionRequest : VersionRequest
{
    [StringLength(150, ErrorMessage = "Prescription name must not exceed 150 characters.")] public string? PrescriptionName { get; set; }
    [Range(1, 10000, ErrorMessage = "The number of packs must be an integer between 1 and 10,000.")] public int TotalDoses { get; set; } = 1;
    [Range(1, 10000, ErrorMessage = "The number of days must be an integer between 1 and 10,000.")] public int? DosageDays { get; set; }
    [StringLength(10000, ErrorMessage = "Usage instructions must not exceed 10,000 characters.")] public string? UsageInstructions { get; set; }
    [StringLength(10000, ErrorMessage = "Notes must not exceed 10,000 characters.")] public string? Note { get; set; }
    [Required(ErrorMessage = "The herb list must not be empty."), MaxLength(500, ErrorMessage = "The value exceeds the maximum allowed length.")] public List<PrescriptionItemRequest> Items { get; set; } = [];
}

public sealed class LoadFormulaRequest : VersionRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Invalid formula identifier.")] public int HerbalFormulaId { get; set; }
}

public sealed class PlanItemRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Invalid service identifier.")] public int ServiceId { get; set; }
    [Range(1, 10000, ErrorMessage = "Service quantity must be an integer between 1 and 10,000.")] public int PlannedQuantity { get; set; } = 1;
    [StringLength(10000, ErrorMessage = "Instructions must not exceed 10,000 characters.")] public string? Instructions { get; set; }
}

public sealed class SaveTreatmentPlanRequest : VersionRequest
{
    [Required(ErrorMessage = "The indication or treatment plan name is required."), StringLength(200, ErrorMessage = "Indication name must not exceed 200 characters.")] public string PlanName { get; set; } = "";
    [Required(ErrorMessage = "This field is required."), RegularExpression("^(SINGLE|COURSE)$", ErrorMessage = "Treatment type must be SINGLE or COURSE.")] public string TreatmentType { get; set; } = "SINGLE";
    [Range(1, 10000, ErrorMessage = "The total number of sessions must be an integer between 1 and 10,000.")] public int PlannedSessionCount { get; set; } = 1;
    [StringLength(10000, ErrorMessage = "Description must not exceed 10,000 characters.")] public string? Description { get; set; }
    [Required(ErrorMessage = "The service list must not be empty."), MaxLength(500, ErrorMessage = "The value exceeds the maximum allowed length.")] public List<PlanItemRequest> Items { get; set; } = [];
    [MaxLength(10000, ErrorMessage = "The value exceeds the maximum allowed length.")] public List<PlannedSessionRequest>? Sessions { get; set; }
}

public sealed class PlannedSessionRequest
{
    [Range(1, 10000, ErrorMessage = "Session number must be an integer between 1 and 10,000.")] public int SessionNumber { get; set; }
    [Required(ErrorMessage = "The session service list must not be empty."), MaxLength(500, ErrorMessage = "The value exceeds the maximum allowed length.")] public List<int> ServiceIds { get; set; } = [];
}
