using System.ComponentModel.DataAnnotations;

namespace COSGeniusSystem.DTOs.Compatibility.V1;

/// <summary>Compatibility request for CreatePatientV1Async. Current HTTP API uses Patients.Requests.</summary>
public sealed class CreatePatientRequest
{
    [Required(ErrorMessage = "This field is required.")]
    [StringLength(150, ErrorMessage = "Invalid data length.")]
    public string FullName { get; set; } = null!;

    [StringLength(10, ErrorMessage = "Invalid data length.")]
    public string? Gender { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    [StringLength(20, ErrorMessage = "Invalid data length.")]
    public string? Phone { get; set; }

    [StringLength(255, ErrorMessage = "Invalid data length.")]
    public string? Address { get; set; }

    [StringLength(20, ErrorMessage = "Invalid data length.")]
    public string? IdentityCard { get; set; }

    [StringLength(100, ErrorMessage = "Invalid data length.")]
    [EmailAddress(ErrorMessage = "Invalid email address format.")]
    public string? Email { get; set; }

    public DateOnly? IdentityCardIssuedDate { get; set; }

    [StringLength(255, ErrorMessage = "Invalid data length.")]
    public string? IdentityCardIssuedPlace { get; set; }

    [StringLength(500, ErrorMessage = "Invalid data length.")]
    public string? PhotoUrl { get; set; }

    [StringLength(100, ErrorMessage = "Invalid data length.")]
    public string? Province { get; set; }

    [StringLength(100, ErrorMessage = "Invalid data length.")]
    public string? Ward { get; set; }

    [StringLength(150, ErrorMessage = "Invalid data length.")]
    public string? Occupation { get; set; }

    [StringLength(255, ErrorMessage = "Invalid data length.")]
    public string? Workplace { get; set; }

    [StringLength(100, ErrorMessage = "Invalid data length.")]
    public string? Nationality { get; set; }

    [StringLength(100, ErrorMessage = "Invalid data length.")]
    public string? Ethnicity { get; set; }

    // Thông tin người liên hệ đại diện
    [StringLength(150, ErrorMessage = "Invalid data length.")]
    public string? ContactFullName { get; set; }

    [StringLength(50, ErrorMessage = "Invalid data length.")]
    public string? ContactRelationship { get; set; }

    [StringLength(20, ErrorMessage = "Invalid data length.")]
    public string? ContactPhone { get; set; }
}
