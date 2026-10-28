namespace COSGeniusSystem.DTOs.Patients;

public sealed class TreatmentProtocolDto
{
    public int ProtocolId { get; init; }
    public string ProtocolCode { get; init; } = null!;
    public string ProtocolName { get; init; } = null!;
    public string DoctorName { get; init; } = null!;
    public DateOnly StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public int CompletedSessions { get; init; }
    public int TotalSessions { get; init; }
    public string Status { get; init; } = null!;
}

