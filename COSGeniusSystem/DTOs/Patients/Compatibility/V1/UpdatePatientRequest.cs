using System.ComponentModel.DataAnnotations;

namespace COSGeniusSystem.DTOs.Compatibility.V1;

/// <summary>Compatibility request for UpdatePatientV1Async. Current HTTP API uses Patients.Requests.</summary>
public sealed class UpdatePatientRequest
{
    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(150, ErrorMessage = "Full name must not exceed 150 characters.")]
    public string FullName { get; set; } = null!;

    [StringLength(10, ErrorMessage = "Invalid data length.")]
    public string? Gender { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    [StringLength(20, ErrorMessage = "Invalid data length.")]
    [RegularExpression(@"^(0[35789]\d{8}|0\d{9})?$", ErrorMessage = "Phone number must contain 10 digits and start with 0.")]
    public string? Phone { get; set; }

    [StringLength(255, ErrorMessage = "Invalid data length.")]
    public string? Address { get; set; }

    [StringLength(20, ErrorMessage = "Invalid data length.")]
    [RegularExpression(@"^(\d{9}|\d{12})?$", ErrorMessage = "Identity card number must contain 9 or 12 digits.")]
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

    // Danh sách người liên hệ cần cập nhật/bổ sung
    public List<ContactUpdateItem>? Contacts { get; set; }
}

/// <summary>Compatibility request for UpdatePatientV1Async. Current HTTP API uses Patients.Requests.</summary>
public sealed class ContactUpdateItem
{
    public int? ContactId { get; set; }

    [Required(ErrorMessage = "Contact full name is required.")]
    [StringLength(150, ErrorMessage = "Contact full name must not exceed 150 characters.")]
    public string FullName { get; set; } = null!;

    [StringLength(50, ErrorMessage = "Invalid data length.")]
    public string? Relationship { get; set; }

    [StringLength(20, ErrorMessage = "Invalid data length.")]
    [RegularExpression(@"^(0[35789]\d{8}|0\d{9})?$", ErrorMessage = "Contact phone number must contain 10 digits and start with 0.")]
    public string? Phone { get; set; }
}
