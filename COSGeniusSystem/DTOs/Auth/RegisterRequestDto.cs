using System.ComponentModel.DataAnnotations;

namespace COSGeniusSystem.DTOs;

public class RegisterRequestDto
{
    [Required(ErrorMessage = "Username is required.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must contain between 3 and 50 characters.")]
    public string Username { get; set; } = null!;

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must contain at least 6 characters.")]
    public string Password { get; set; } = null!;

    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(150, ErrorMessage = "Full name must not exceed 150 characters.")]
    public string FullName { get; set; } = null!;

    [EmailAddress(ErrorMessage = "Invalid email address format.")]
    [StringLength(100, ErrorMessage = "Email must not exceed 100 characters.")]
    public string? Email { get; set; }

    [Phone(ErrorMessage = "Invalid phone number format.")]
    [StringLength(20, ErrorMessage = "Phone number must not exceed 20 characters.")]
    public string? Phone { get; set; }

    [StringLength(50, ErrorMessage = "Role code must not exceed 50 characters.")]
    public string? RoleCode { get; set; }
}
