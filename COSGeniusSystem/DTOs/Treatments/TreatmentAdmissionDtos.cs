using System.ComponentModel.DataAnnotations;

namespace COSGeniusSystem.DTOs;

public sealed class AdmissionItem : VersionRequest
{
    [Range(1, int.MaxValue)] public int PlanId { get; set; }
    [Range(1, int.MaxValue)] public int AssignedDoctorId { get; set; }
}
public sealed class CreateTreatmentAdmission
{
    public Guid RegistrationKey { get; set; }
    [Range(1, int.MaxValue)] public int PatientId { get; set; }
    [Required] public DateOnly? ScheduledDate { get; set; }
    [Required, RegularExpression("^(MORNING|AFTERNOON)$")] public string Shift { get; set; } = "";
    [StringLength(1000)] public string? Note { get; set; }
    [Required, MinLength(1), MaxLength(100)] public List<AdmissionItem> Items { get; set; } = [];
}
public sealed record AdmissionSummary(Guid? AdmissionId, int? LegacySessionId, int PatientId,
    string PatientName, string? PatientCode, string? Phone, int? DoctorId, string? DoctorName,
    DateOnly? ScheduledDate, string? Shift, DateTime CreatedAt, DateTime? AcceptedAt,
    int RequestCount, int CompletedCount, string Status, byte[] RowVersion);
public sealed record AdmissionOverview(AdmissionSummary Group, IReadOnlyList<SessionSummary> Requests);
