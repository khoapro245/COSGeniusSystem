using System.ComponentModel.DataAnnotations;

namespace COSGeniusSystem.DTOs;

public class UpdateProfileRequestDto
{
    [Required(ErrorMessage = "Họ và tên không được để trống.")]
    [MaxLength(150, ErrorMessage = "Họ và tên không được vượt quá 150 ký tự.")]
    public string FullName { get; set; } = null!;

    [MaxLength(100, ErrorMessage = "Email không được vượt quá 100 ký tự.")]
    [EmailAddress(ErrorMessage = "Định dạng email không hợp lệ.")]
    public string? Email { get; set; }

    [MaxLength(20, ErrorMessage = "Số điện thoại không được vượt quá 20 ký tự.")]
    [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
    public string? Phone { get; set; }

    public string? CurrentPassword { get; set; }

    [MinLength(6, ErrorMessage = "Mật khẩu mới phải có ít nhất 6 ký tự.")]
    public string? NewPassword { get; set; }
}

