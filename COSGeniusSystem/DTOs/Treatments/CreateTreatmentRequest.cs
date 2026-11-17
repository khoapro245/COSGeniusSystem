using System.ComponentModel.DataAnnotations;
using COSGeniusSystem.DTOs;

namespace COSGeniusSystem.DTOs;

public sealed class CreateTreatmentRequest : VersionRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "The value is outside the allowed range.")] public int PlanId { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "The value is outside the allowed range.")] public int AssignedDoctorId { get; set; }
    [Required(ErrorMessage = "This field is required.")] public DateOnly? ScheduledDate { get; set; }
}
