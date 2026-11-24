namespace COSGeniusSystem.DTOs;

public class DoctorResponseDto
{
    public int DoctorId { get; set; }
    public int? UserId { get; set; }
    public string FullName { get; set; } = null!;
    public string? Specialization { get; set; }
    public string? LicenseNumber { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; }
    public string? Username { get; set; }
    public string? Email { get; set; }
}
