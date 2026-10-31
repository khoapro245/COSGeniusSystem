using System.ComponentModel.DataAnnotations;
using COSGeniusSystem.DTOs.Examinations;
using COSGeniusSystem.DTOs.Patients.Responses;

namespace COSGeniusSystem.DTOs.Treatments;

public sealed class PlanQuery : PageQuery
{
    [Range(1, int.MaxValue)] public int? PatientId { get; set; }
    [Range(1, int.MaxValue)] public int? DoctorId { get; set; }
    [RegularExpression("^(DRAFT|CONFIRMED|IN_PROGRESS|COMPLETED|CANCELLED)$")] public string? Status { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
}
public sealed class SessionQuery : PageQuery
{
    [Range(1, int.MaxValue)] public int? PlanId { get; set; }
    [Range(1, int.MaxValue)] public int? DoctorId { get; set; }
    [RegularExpression("^(PLANNED|REQUESTED|IN_PROGRESS|COMPLETED|CANCELLED)$")] public string? Status { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
}
public sealed class RequestTreatment : VersionRequest
{
    [Range(1, int.MaxValue)] public int AssignedDoctorId { get; set; }
    [Required] public DateOnly? ScheduledDate { get; set; }
}
public sealed class CancelTreatment : VersionRequest
{
    [Required, StringLength(500, MinimumLength = 1)] public string Reason { get; set; } = "";
}
public sealed class UsageInput
{
    [Range(1, int.MaxValue)] public int PlanDetailId { get; set; }
    [Range(1, 10000)] public int Quantity { get; set; }
    [StringLength(500)] public string? ResultNote { get; set; }
}
public sealed class CompleteTreatment : VersionRequest
{
    [Required, StringLength(10000, MinimumLength = 1)] public string ResultNote { get; set; } = "";
    [Required, MinLength(1), MaxLength(500)] public List<UsageInput> Items { get; set; } = [];
}
public sealed record PlanSummary(int PlanId, string? PlanCode, string PlanName, int RecordId,
    int PatientId, string? PatientCode, string PatientName, string? Phone, int DoctorId, string DoctorName,
    string Status, DateTime CreatedAt, int PlannedSessionCount, int CompletedSessions, int ActiveRequests, byte[] RowVersion);
public sealed record ServiceProgress(int PlanDetailId, int ServiceId, string ServiceName, string Unit,
    decimal UnitPrice, int PlannedQuantity, int UsedQuantity, int RemainingQuantity, string? Instructions);
public sealed record PlanOverview(PlanSummary Plan, PatientResponse Patient, int RegistrationId,
    DateTime ExamDate, string? Diagnosis, string? AllergyNote, string? Description, string TreatmentType,
    DateOnly? StartDate, DateOnly? EndDate, int RemainingSessions, bool FullyPaid, bool RequiresFullPayment,
    bool CanRequest, bool CanCancel, IReadOnlyList<ServiceProgress> Services);
public sealed record SessionSummary(int SessionId, int PlanId, string? PlanCode, string PlanName,
    int SessionNumber, int PatientId, string? PatientCode, string PatientName, string? Phone,
    int? AssignedDoctorId, string? AssignedDoctorName, int? PerformedBy, string? PerformedByName,
    int? RequestedBy, DateTime? RequestedAt, DateOnly? ScheduledDate, DateTime? StartedAt,
    DateTime? CompletedAt, string Status, string? ResultNote, byte[] RowVersion);
public sealed record UsageResponse(int PlanDetailId, string ServiceName, int Quantity, int PerformedBy,
    DateTime ExecutionTime, string? ResultNote);
public sealed record SessionOverview(SessionSummary Session, PlanOverview TreatmentPlan,
    IReadOnlyList<UsageResponse> Usage, bool CanStart, bool CanComplete, bool CanCancel);
