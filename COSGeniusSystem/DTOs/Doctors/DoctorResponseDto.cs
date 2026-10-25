namespace COSGeniusSystem.DTOs.Doctors;

public class DoctorResponseDto
{
    public int DoctorId { get; set; }
    public string FullName { get; set; } = null!;
    public string? Specialization { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; }
}
