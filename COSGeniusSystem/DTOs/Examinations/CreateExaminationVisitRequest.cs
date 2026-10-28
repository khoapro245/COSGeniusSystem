using System.ComponentModel.DataAnnotations;

namespace COSGeniusSystem.DTOs.Examinations;

public sealed class CreateExaminationVisitRequest
{
    [Required]
    public int PatientId { get; set; }

    [Required]
    public int DoctorId { get; set; }

    public string? Symptoms { get; set; }

    public string? DoctorAdvice { get; set; }

    public bool IsPriority { get; set; }
}

