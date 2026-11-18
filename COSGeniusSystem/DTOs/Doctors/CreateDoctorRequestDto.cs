using System.ComponentModel.DataAnnotations;

namespace COSGeniusSystem.DTOs;

public class CreateDoctorRequestDto
{
    public int? UserId { get; set; }

    [Required(ErrorMessage = "Họ và tên bác sĩ là bắt buộc.")]
    [StringLength(150, ErrorMessage = "Họ và tên không được vượt quá 150 ký tự.")]
    public string FullName { get; set; } = null!;

    [StringLength(100, ErrorMessage = "Chuyên khoa không được vượt quá 100 ký tự.")]
    public string? Specialization { get; set; }

    [StringLength(50, ErrorMessage = "Số chứng chỉ hành nghề không được vượt quá 50 ký tự.")]
    public string? LicenseNumber { get; set; }

    [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
    [StringLength(20, ErrorMessage = "Số điện thoại không được vượt quá 20 ký tự.")]
    public string? Phone { get; set; }

    public bool IsActive { get; set; } = true;
}

