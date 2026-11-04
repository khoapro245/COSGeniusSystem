using System.ComponentModel.DataAnnotations;
using COSGeniusSystem.DTOs;

namespace COSGeniusSystem.DTOs;

public sealed class CreateTreatmentRequest : VersionRequest
{
    [Range(1, int.MaxValue)] public int PlanId { get; set; }
    [Range(1, int.MaxValue)] public int AssignedDoctorId { get; set; }
    [Required] public DateOnly? ScheduledDate { get; set; }
}
