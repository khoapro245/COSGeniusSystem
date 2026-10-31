using System.ComponentModel.DataAnnotations;

namespace COSGeniusSystem.DTOs.Examinations;

public class PageQuery
{
    [Range(1, 1000000)] public int PageNumber { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
    [StringLength(150)] public string? Search { get; set; }
}

public sealed class RegistrationQuery : PageQuery
{
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    [Range(1, int.MaxValue)] public int? DoctorId { get; set; }
    [RegularExpression("^(WAITING|IN_PROGRESS|COMPLETED|CANCELLED)$")] public string? Status { get; set; }
}

public class RegistrationInput
{
    [Range(1, int.MaxValue)] public int DoctorId { get; set; }
    [Range(1, int.MaxValue)] public int? ContactId { get; set; }
    public bool IsPriority { get; set; }
    [StringLength(10000)] public string? Symptoms { get; set; }
    [StringLength(10000)] public string? Note { get; set; }
}

public sealed class CreateRegistrationRequest : RegistrationInput
{
    [Range(1, int.MaxValue)] public int PatientId { get; set; }
}

public sealed class UpdateRegistrationRequest : RegistrationInput
{
    [Required, MinLength(8), MaxLength(8)] public byte[] RowVersion { get; set; } = [];
}

public class VersionRequest
{
    [Required, MinLength(8), MaxLength(8)] public byte[] RowVersion { get; set; } = [];
}

public class MedicalRecordData
{
    [StringLength(10000)] public string? Symptoms { get; set; }
    [StringLength(500)] public string? PreliminaryDiagnosis { get; set; }
    [StringLength(500)] public string? FinalDiagnosis { get; set; }
    [StringLength(10000)] public string? DoctorAdvice { get; set; }
    [Range(1, 400)] public short? PulseRate { get; set; }
    [Range(20, 50)] public decimal? Temperature { get; set; }
    [Range(1, 150)] public short? RespiratoryRate { get; set; }
    [Range(1, 300)] public decimal? HeightCm { get; set; }
    [Range(0.1, 1000)] public decimal? WeightKg { get; set; }
    [Range(0, 100)] public decimal? SpO2 { get; set; }
    [StringLength(10000)] public string? Inspection { get; set; }
    [StringLength(10000)] public string? ListeningSmelling { get; set; }
    [StringLength(10000)] public string? Inquiry { get; set; }
    [StringLength(10000)] public string? Palpation { get; set; }
    [StringLength(10000)] public string? FourExaminationSummary { get; set; }
    [StringLength(10000)] public string? SyndromeDifferentiation { get; set; }
    [StringLength(10000)] public string? TraditionalDiagnosis { get; set; }
    [StringLength(20)] public string? Icdcode { get; set; }
    [StringLength(500)] public string? Icddescription { get; set; }
}

public sealed class SaveMedicalRecordRequest : MedicalRecordData
{
    [Required, MinLength(8), MaxLength(8)] public byte[] RowVersion { get; set; } = [];
}

public sealed class PrescriptionItemRequest
{
    [Range(1, int.MaxValue)] public int HerbId { get; set; }
    [Range(typeof(decimal), "0.001", "999999.999")] public decimal DosagePerDose { get; set; }
    [StringLength(50)] public string? RoleInFormula { get; set; }
    [StringLength(255)] public string? Note { get; set; }
}

public sealed class SavePrescriptionRequest : VersionRequest
{
    [StringLength(150)] public string? PrescriptionName { get; set; }
    [Range(1, 10000)] public int TotalDoses { get; set; } = 1;
    [Range(1, 10000)] public int? DosageDays { get; set; }
    [StringLength(10000)] public string? UsageInstructions { get; set; }
    [StringLength(10000)] public string? Note { get; set; }
    [Required, MaxLength(500)] public List<PrescriptionItemRequest> Items { get; set; } = [];
}

public sealed class LoadFormulaRequest : VersionRequest
{
    [Range(1, int.MaxValue)] public int HerbalFormulaId { get; set; }
}

public sealed class PlanItemRequest
{
    [Range(1, int.MaxValue)] public int ServiceId { get; set; }
    [Range(1, 10000)] public int PlannedQuantity { get; set; } = 1;
    [StringLength(10000)] public string? Instructions { get; set; }
}

public sealed class SaveTreatmentPlanRequest : VersionRequest
{
    [Required, StringLength(200)] public string PlanName { get; set; } = "";
    [Required, RegularExpression("^(SINGLE|COURSE)$")] public string TreatmentType { get; set; } = "SINGLE";
    [Range(1, 10000)] public int PlannedSessionCount { get; set; } = 1;
    [StringLength(10000)] public string? Description { get; set; }
    [Required, MaxLength(500)] public List<PlanItemRequest> Items { get; set; } = [];
}

