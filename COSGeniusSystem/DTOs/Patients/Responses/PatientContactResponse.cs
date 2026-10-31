namespace COSGeniusSystem.DTOs.Patients.Responses;

public sealed class PatientContactResponse
{
    public int ContactId { get; init; }
    public int PatientId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string? Relationship { get; init; }
    public string? Phone { get; init; }
}
