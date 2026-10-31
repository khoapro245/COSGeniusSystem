using COSGeniusSystem.DTOs.Patients.Responses;

namespace COSGeniusSystem.DTOs.Examinations;

public sealed record RegistrationResponse(int RegistrationId, string? RegistrationCode, int PatientId,
    string? PatientCode, string PatientName, string? Phone, int DoctorId, string DoctorName,
    int RegisteredBy, DateTime RegisteredAt, DateOnly BusinessDate, int QueueNumber, bool IsPriority,
    int? ContactId, string? ContactName, string? Symptoms, string? Note, string Status,
    DateTime? StartedAt, DateTime? CompletedAt, int? RecordId, byte[] RowVersion);

public sealed class MedicalRecordResponse : MedicalRecordData
{
    public int RecordId { get; set; }
    public int RegistrationId { get; set; }
    public int PatientId { get; set; }
    public int DoctorId { get; set; }
    public DateTime ExamDate { get; set; }
    public string Status { get; set; } = "";
    public string? AllergyNoteSnapshot { get; set; }
    public DateTime? CompletedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed record ExaminationResponse(RegistrationResponse Registration, PatientResponse Patient,
    MedicalRecordResponse? MedicalRecord, IReadOnlyList<PrescriptionResponse> Prescriptions,
    IReadOnlyList<TreatmentPlanResponse> TreatmentPlans);

public sealed record PrescriptionItemResponse(int PrescriptionDetailId, int HerbId, string HerbNameSnapshot,
    decimal DosagePerDose, decimal TotalQuantity, string? RoleInFormula, string? Note);

public sealed record PrescriptionResponse(int PrescriptionId, string? PrescriptionCode, int RecordId,
    int? SourceHerbalFormulaId, string? PrescriptionName, int TotalDoses, int? DosageDays,
    string? UsageInstructions, string? Note, string Status, DateTime? SentAt, byte[] RowVersion,
    IReadOnlyList<PrescriptionItemResponse> Items);

public sealed record PlanItemResponse(int PlanDetailId, int ServiceId, string ServiceNameSnapshot,
    string UnitSnapshot, decimal UnitPrice, int PlannedQuantity, string? Instructions);

public sealed record TreatmentPlanResponse(int PlanId, string? PlanCode, int RecordId, int DoctorId,
    string PlanName, string TreatmentType, int PlannedSessionCount, string? Description, string Status,
    DateTime? ConfirmedAt, byte[] RowVersion, IReadOnlyList<PlanItemResponse> Items);

public sealed record FormulaItemResponse(int HerbId, string HerbName, decimal DosagePerDose,
    string? RoleInFormula, string? Note, bool IsActive);

public sealed record FormulaResponse(int HerbalFormulaId, string? FormulaCode, string FormulaName,
    string? IntendedUse, string? UsageInstructions, IReadOnlyList<FormulaItemResponse> Items);
