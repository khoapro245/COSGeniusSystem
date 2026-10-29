using System.ComponentModel.DataAnnotations;

namespace COSGeniusSystem.DTOs.Treatments;

public sealed class CreateTreatmentRequest
{
    [Required]
    public int PatientId { get; set; }

    [Required]
    public int ServiceId { get; set; }

    [Required]
    public int DoctorId { get; set; }

    public int? ExamSheetId { get; set; }

    public string? Note { get; set; }
}

