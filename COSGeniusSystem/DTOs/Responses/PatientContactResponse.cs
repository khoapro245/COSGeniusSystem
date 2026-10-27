namespace COSGeniusSystem.DTOs.Responses;

public sealed class PatientContactResponse
{
    public int ContactId { get; init; }
    public int PatientId { get; init; }
    public string FullName { get; init; } = null!;
    public string? Relationship { get; init; }
    public string? Phone { get; init; }
}
