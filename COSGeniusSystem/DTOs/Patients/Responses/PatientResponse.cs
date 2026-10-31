namespace COSGeniusSystem.DTOs.Patients.Responses;

public sealed class PatientResponse
{
    public int PatientId { get; init; }
    public string? PatientCode { get; init; }
    public DateTime CreatedAt { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string? Gender { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public string? Phone { get; init; }
    public string? Address { get; init; }
    public string? IdentityCard { get; init; }
    public string? Email { get; init; }
    public DateOnly? IdentityCardIssuedDate { get; init; }
    public string? IdentityCardIssuedPlace { get; init; }
    public string? PhotoUrl { get; init; }
    public string? Province { get; init; }
    public string? Ward { get; init; }
    public string? Occupation { get; init; }
    public string? Workplace { get; init; }
    public string? Nationality { get; init; }
    public string? Ethnicity { get; init; }
}
