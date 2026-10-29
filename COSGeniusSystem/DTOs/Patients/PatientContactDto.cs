namespace COSGeniusSystem.DTOs.Patients;

public sealed class PatientContactDto
{
    public int ContactId { get; init; }
    public string FullName { get; init; } = null!;
    public string? Relationship { get; init; }
    public string? Phone { get; init; }
}

