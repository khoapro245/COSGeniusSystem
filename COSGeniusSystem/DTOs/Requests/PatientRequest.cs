using System.ComponentModel.DataAnnotations;

namespace COSGeniusSystem.DTOs.Requests;

public abstract class PatientRequest
{
    [Required]
    [StringLength(150)]
    public string FullName { get; set; } = null!;

    [StringLength(10)]
    public string? Gender { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    [StringLength(20)]
    public string? Phone { get; set; }

    [StringLength(255)]
    public string? Address { get; set; }

    [StringLength(20)]
    public string? IdentityCard { get; set; }

    [StringLength(100)]
    [EmailAddress]
    public string? Email { get; set; }

    public DateOnly? IdentityCardIssuedDate { get; set; }

    [StringLength(255)]
    public string? IdentityCardIssuedPlace { get; set; }

    [StringLength(500)]
    public string? PhotoUrl { get; set; }

    [StringLength(100)]
    public string? Province { get; set; }

    [StringLength(100)]
    public string? Ward { get; set; }

    [StringLength(150)]
    public string? Occupation { get; set; }

    [StringLength(255)]
    public string? Workplace { get; set; }

    [StringLength(100)]
    public string? Nationality { get; set; }

    [StringLength(100)]
    public string? Ethnicity { get; set; }

}
