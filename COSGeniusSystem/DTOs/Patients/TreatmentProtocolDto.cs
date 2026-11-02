namespace COSGeniusSystem.DTOs;

public sealed class TreatmentProtocolDto
{
    public int PlanId { get; init; }
    public string? PlanCode { get; init; } = null!;
    public string PlanName { get; init; } = null!;
    public string DoctorName { get; init; } = null!;
    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public int CompletedSessions { get; init; }
    public int TotalSessions { get; init; }
    public string Status { get; init; } = null!;
}

