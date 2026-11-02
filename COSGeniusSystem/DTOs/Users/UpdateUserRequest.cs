using System.ComponentModel.DataAnnotations;

namespace COSGeniusSystem.DTOs;

public class UpdateUserRequest
{
    [Required(ErrorMessage = "FullName is required.")]
    [MaxLength(150, ErrorMessage = "FullName must not exceed 150 characters.")]
    public string FullName { get; set; } = null!;

    [MaxLength(100, ErrorMessage = "Email must not exceed 100 characters.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    public string? Email { get; set; }

    [MaxLength(20, ErrorMessage = "Phone must not exceed 20 characters.")]
    public string? Phone { get; set; }

    /// <summary>Gán lại vai trò mới cho tài khoản (chọn từ 7 vai trò).</summary>
    public string? RoleCode { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Đổi mật khẩu mới nếu được truyền vào; để trống nếu giữ mật khẩu cũ.</summary>
    [MinLength(6, ErrorMessage = "New password must be at least 6 characters.")]
    public string? NewPassword { get; set; }
}

