using System.ComponentModel.DataAnnotations;

namespace COSGeniusSystem.DTOs;

public abstract class PatientRequest
{
    public bool ConfirmDuplicatePhone { get; set; }
    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(100, ErrorMessage = "Full name must not exceed 100 characters.")]
    public string FullName { get; set; } = string.Empty;

    [StringLength(20, ErrorMessage = "Gender must not exceed 20 characters.")]
    public string? Gender { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    [StringLength(20, ErrorMessage = "Phone number must not exceed 20 characters.")]
    public string? Phone { get; set; }

    [StringLength(255, ErrorMessage = "Address must not exceed 255 characters.")]
    public string? Address { get; set; }

    [StringLength(20, ErrorMessage = "Identity card number must not exceed 20 characters.")]
    [RegularExpression(@"^(\d{9}|\d{12})?$", ErrorMessage = "Identity card number must contain 9 or 12 digits.")]
    public string? IdentityCard { get; set; }

    [StringLength(100, ErrorMessage = "Email must not exceed 100 characters.")]
    [EmailAddress(ErrorMessage = "Invalid email address format.")]
    public string? Email { get; set; }

    public DateOnly? IdentityCardIssuedDate { get; set; }

    [StringLength(255, ErrorMessage = "Identity card place of issue must not exceed 255 characters.")]
    public string? IdentityCardIssuedPlace { get; set; }

    [StringLength(500, ErrorMessage = "Image path must not exceed 500 characters.")]
    public string? PhotoUrl { get; set; }

    [StringLength(100, ErrorMessage = "Province or city must not exceed 100 characters.")]
    public string? Province { get; set; }

    [StringLength(100, ErrorMessage = "Ward or commune must not exceed 100 characters.")]
    public string? Ward { get; set; }

    [StringLength(150, ErrorMessage = "Occupation must not exceed 150 characters.")]
    public string? Occupation { get; set; }

    [StringLength(255, ErrorMessage = "Workplace must not exceed 255 characters.")]
    public string? Workplace { get; set; }

    [StringLength(100, ErrorMessage = "Nationality must not exceed 100 characters.")]
    public string? Nationality { get; set; }

    [StringLength(100, ErrorMessage = "Ethnicity must not exceed 100 characters.")]
    public string? Ethnicity { get; set; }
}
