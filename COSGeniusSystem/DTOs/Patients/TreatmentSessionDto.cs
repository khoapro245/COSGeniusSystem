namespace COSGeniusSystem.DTOs.Patients;

public sealed class TreatmentSessionDto
{
    public int SessionId { get; init; }
    public int SessionNo { get; init; }
    public DateTime? ExecutionTime { get; init; }

    public string? DoctorName { get; init; }
    public string? ResultNote { get; init; }
    public string Status { get; init; } = null!;
}

