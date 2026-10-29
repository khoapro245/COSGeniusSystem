namespace COSGeniusSystem.DTOs.Patients;

public sealed class PatientResponseDto
{
    public int PatientId { get; init; }
    public string? PatientCode { get; init; }
    public string FullName { get; init; } = null!;
    public DateOnly? DateOfBirth { get; init; }
    public string? Gender { get; init; }
    public string? Phone { get; init; }
    public string? Address { get; init; }
}

