using COSGeniusSystem.DTOs.Reception;

namespace COSGeniusSystem.Services.Reception;

public interface IReceptionDashboardService
{
    Task<DashboardStatsDto> GetTodayStatsAsync(CancellationToken ct = default);
    Task<DashboardQueueDto> GetQuickQueuesAsync(int top = 3, CancellationToken ct = default);
}

