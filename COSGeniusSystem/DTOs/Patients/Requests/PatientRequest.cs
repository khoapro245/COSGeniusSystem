using System.ComponentModel.DataAnnotations;

namespace COSGeniusSystem.DTOs;

public abstract class PatientRequest
{
    public bool ConfirmDuplicatePhone { get; set; }
    [Required(ErrorMessage = "Họ và tên là bắt buộc.")]
    [StringLength(100, ErrorMessage = "Họ và tên không được vượt quá 100 ký tự.")]
    public string FullName { get; set; } = string.Empty;

    [StringLength(20, ErrorMessage = "Giới tính không được vượt quá 20 ký tự.")]
    public string? Gender { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    [StringLength(20, ErrorMessage = "Số điện thoại không được vượt quá 20 ký tự.")]
    public string? Phone { get; set; }

    [StringLength(255, ErrorMessage = "Địa chỉ không được vượt quá 255 ký tự.")]
    public string? Address { get; set; }

    [StringLength(20, ErrorMessage = "Số CCCD/CMND không được vượt quá 20 ký tự.")]
    [RegularExpression(@"^(\d{9}|\d{12})?$", ErrorMessage = "Số CCCD/CMND phải gồm 9 hoặc 12 chữ số.")]
    public string? IdentityCard { get; set; }

    [StringLength(100, ErrorMessage = "Email không được vượt quá 100 ký tự.")]
    [EmailAddress(ErrorMessage = "Địa chỉ email không đúng định dạng.")]
    public string? Email { get; set; }

    public DateOnly? IdentityCardIssuedDate { get; set; }

    [StringLength(255, ErrorMessage = "Nơi cấp CCCD không được vượt quá 255 ký tự.")]
    public string? IdentityCardIssuedPlace { get; set; }

    [StringLength(500, ErrorMessage = "Đường dẫn ảnh không được vượt quá 500 ký tự.")]
    public string? PhotoUrl { get; set; }

    [StringLength(100, ErrorMessage = "Tỉnh/Thành phố không được vượt quá 100 ký tự.")]
    public string? Province { get; set; }

    [StringLength(100, ErrorMessage = "Phường/Xã không được vượt quá 100 ký tự.")]
    public string? Ward { get; set; }

    [StringLength(150, ErrorMessage = "Nghề nghiệp không được vượt quá 150 ký tự.")]
    public string? Occupation { get; set; }

    [StringLength(255, ErrorMessage = "Nơi làm việc không được vượt quá 255 ký tự.")]
    public string? Workplace { get; set; }

    [StringLength(100, ErrorMessage = "Quốc tịch không được vượt quá 100 ký tự.")]
    public string? Nationality { get; set; }

    [StringLength(100, ErrorMessage = "Dân tộc không được vượt quá 100 ký tự.")]
    public string? Ethnicity { get; set; }
}
