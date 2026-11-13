using System.ComponentModel.DataAnnotations;

namespace COSGeniusSystem.DTOs.Compatibility.V1;

/// <summary>Compatibility request for UpdatePatientV1Async. Current HTTP API uses Patients.Requests.</summary>
public sealed class UpdatePatientRequest
{
    [Required(ErrorMessage = "Họ và tên là bắt buộc.")]
    [StringLength(150, ErrorMessage = "Họ và tên không được vượt quá 150 ký tự.")]
    public string FullName { get; set; } = null!;

    [StringLength(10)]
    public string? Gender { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    [StringLength(20)]
    [RegularExpression(@"^(0[35789]\d{8}|0\d{9})?$", ErrorMessage = "Số điện thoại phải gồm 10 chữ số bắt đầu bằng 0.")]
    public string? Phone { get; set; }

    [StringLength(255)]
    public string? Address { get; set; }

    [StringLength(20)]
    [RegularExpression(@"^(\d{9}|\d{12})?$", ErrorMessage = "Số CCCD/CMND phải gồm 9 hoặc 12 chữ số.")]
    public string? IdentityCard { get; set; }

    [StringLength(100)]
    [EmailAddress(ErrorMessage = "Địa chỉ email không đúng định dạng.")]
    public string? Email { get; set; }

    public DateOnly? IdentityCardIssuedDate { get; set; }

    [StringLength(255)]
    public string? IdentityCardIssuedPlace { get; set; }

    [StringLength(500)]
    public string? PhotoUrl { get; set; }

    [StringLength(100)]
    public string? Province { get; set; }

    [StringLength(100)]
    public string? Ward { get; set; }

    [StringLength(150)]
    public string? Occupation { get; set; }

    [StringLength(255)]
    public string? Workplace { get; set; }

    [StringLength(100)]
    public string? Nationality { get; set; }

    [StringLength(100)]
    public string? Ethnicity { get; set; }

    // Danh sách người liên hệ cần cập nhật/bổ sung
    public List<ContactUpdateItem>? Contacts { get; set; }
}

/// <summary>Compatibility request for UpdatePatientV1Async. Current HTTP API uses Patients.Requests.</summary>
public sealed class ContactUpdateItem
{
    public int? ContactId { get; set; }

    [Required(ErrorMessage = "Họ tên người liên hệ là bắt buộc.")]
    [StringLength(150, ErrorMessage = "Họ tên người liên hệ không được vượt quá 150 ký tự.")]
    public string FullName { get; set; } = null!;

    [StringLength(50)]
    public string? Relationship { get; set; }

    [StringLength(20)]
    [RegularExpression(@"^(0[35789]\d{8}|0\d{9})?$", ErrorMessage = "Số điện thoại người liên hệ phải gồm 10 chữ số bắt đầu bằng 0.")]
    public string? Phone { get; set; }
}
