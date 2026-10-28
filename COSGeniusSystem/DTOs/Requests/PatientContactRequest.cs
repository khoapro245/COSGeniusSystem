using System.ComponentModel.DataAnnotations;

namespace COSGeniusSystem.DTOs.Requests;

public abstract class PatientContactRequest
{
    [Required]
    [StringLength(150)]
    public string FullName { get; set; } = null!;

    [StringLength(50)]
    public string? Relationship { get; set; }

    [StringLength(20)]
    public string? Phone { get; set; }

}
