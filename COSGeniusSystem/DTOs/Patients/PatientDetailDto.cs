namespace COSGeniusSystem.DTOs.Patients;

public sealed class PatientDetailDto
{
    public int PatientId { get; init; }
    public string? PatientCode { get; init; }
    public string FullName { get; init; } = null!;
    public DateOnly? DateOfBirth { get; init; }
    public string? Gender { get; init; }
    public string? Phone { get; init; }
    public string? Address { get; init; }
    public string? IdentityCard { get; init; }
    public DateOnly? IdentityCardIssuedDate { get; init; }
    public string? IdentityCardIssuedPlace { get; init; }
    public string? Email { get; init; }
    public string? Province { get; init; }
    public string? Ward { get; init; }
    public string? Occupation { get; init; }
    public string? Workplace { get; init; }
    public string? Ethnicity { get; init; }
    public string? Nationality { get; init; }
    public string? PhotoUrl { get; init; }
    public DateTime CreatedAt { get; init; }
    public List<PatientContactDto> Contacts { get; init; } = new();
}

