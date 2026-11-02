using System.ComponentModel.DataAnnotations;

namespace COSGeniusSystem.DTOs;

public class CreateUserRequest
{
    [Required(ErrorMessage = "Username is required.")]
    [MinLength(3, ErrorMessage = "Username must be at least 3 characters.")]
    [MaxLength(50, ErrorMessage = "Username must not exceed 50 characters.")]
    public string Username { get; set; } = null!;

    [Required(ErrorMessage = "Password is required.")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
    public string Password { get; set; } = null!;

    [Required(ErrorMessage = "FullName is required.")]
    [MaxLength(150, ErrorMessage = "FullName must not exceed 150 characters.")]
    public string FullName { get; set; } = null!;

    [MaxLength(100, ErrorMessage = "Email must not exceed 100 characters.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    public string? Email { get; set; }

    [MaxLength(20, ErrorMessage = "Phone must not exceed 20 characters.")]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "RoleCode is required.")]
    public string RoleCode { get; set; } = null!;

    public bool IsActive { get; set; } = true;
}

