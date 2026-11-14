using System.ComponentModel.DataAnnotations;

namespace COSGeniusSystem.DTOs;

public class CreatePatientRequest : PatientRequest
{
    [Required(ErrorMessage = "Số điện thoại là bắt buộc.")]
    [COSGeniusSystem.Commons.PatientPhone]
    public new string Phone
    {
        get => base.Phone ?? string.Empty;
        set => base.Phone = value;
    }

    [StringLength(150, ErrorMessage = "Họ tên người liên hệ không được vượt quá 150 ký tự.")]
    public string? ContactFullName { get; set; }
    [StringLength(50, ErrorMessage = "Mối quan hệ không được vượt quá 50 ký tự.")]
    public string? ContactRelationship { get; set; }
    [StringLength(20, ErrorMessage = "Số điện thoại người liên hệ không được vượt quá 20 ký tự.")]
    public string? ContactPhone { get; set; }
}

public class CreatePatientDto : CreatePatientRequest { }
