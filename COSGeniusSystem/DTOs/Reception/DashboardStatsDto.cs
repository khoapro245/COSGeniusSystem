namespace COSGeniusSystem.DTOs.Reception;

public sealed class DashboardStatsDto
{
    public int TodayTotalVisits { get; init; }
    public int WaitingExaminationCount { get; init; }
    public int WaitingTreatmentCount { get; init; }
    public int CompletedVisitsCount { get; init; }

    // Phân rã số ca
    public int NewExaminationCount { get; init; }
    public int TreatmentCount { get; init; }
}

