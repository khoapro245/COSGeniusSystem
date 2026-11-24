namespace COSGeniusSystem.DTOs;

public class UpdateDoctorRequestDto
{
    public string FullName { get; set; } = string.Empty;
    public string? Specialization { get; set; }
    public string? LicenseNumber { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
    public int? UserId { get; set; }
}

