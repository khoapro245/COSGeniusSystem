using System.ComponentModel.DataAnnotations;

namespace COSGeniusSystem.DTOs;

public abstract class PatientContactRequest
{
    [Required(ErrorMessage = "This field is required.")]
    [StringLength(150, ErrorMessage = "Invalid data length.")]
    public string FullName { get; set; } = string.Empty;

    [StringLength(50, ErrorMessage = "Invalid data length.")]
    public string? Relationship { get; set; }

    [StringLength(20, ErrorMessage = "Invalid data length.")]
    public string? Phone { get; set; }

}
