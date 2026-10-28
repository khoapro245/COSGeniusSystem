using System.ComponentModel.DataAnnotations;

namespace COSGeniusSystem.DTOs.Auth;

public class LoginRequestDto
{
    [Required(ErrorMessage = "Username is required.")]
    public string Username { get; set; } = null!;

    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; set; } = null!;
}
