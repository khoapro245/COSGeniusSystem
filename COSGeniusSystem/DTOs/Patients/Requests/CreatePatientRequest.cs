using System.ComponentModel.DataAnnotations;

namespace COSGeniusSystem.DTOs;

public class CreatePatientRequest : PatientRequest
{
    [Required(ErrorMessage = "Phone number is required.")]
    [COSGeniusSystem.Commons.PatientPhone]
    public new string Phone
    {
        get => base.Phone ?? string.Empty;
        set => base.Phone = value;
    }

    [StringLength(150, ErrorMessage = "Contact full name must not exceed 150 characters.")]
    public string? ContactFullName { get; set; }
    [StringLength(50, ErrorMessage = "Relationship must not exceed 50 characters.")]
    public string? ContactRelationship { get; set; }
    [StringLength(20, ErrorMessage = "Contact phone number must not exceed 20 characters.")]
    public string? ContactPhone { get; set; }
}

public class CreatePatientDto : CreatePatientRequest { }
