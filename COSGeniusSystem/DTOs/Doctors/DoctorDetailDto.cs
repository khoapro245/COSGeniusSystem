namespace COSGeniusSystem.DTOs.Doctors;

public class DoctorDetailDto
{
    public int DoctorId { get; set; }
    public string FullName { get; set; } = null!;
    public string? Specialization { get; set; }
    public string? LicenseNumber { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; }

    // Thông tin user liên kết
    public string? Username { get; set; }
    public string? Email { get; set; }
}
