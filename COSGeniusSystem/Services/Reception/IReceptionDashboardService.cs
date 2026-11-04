using COSGeniusSystem.DTOs;

namespace COSGeniusSystem.Services;

public interface IReceptionDashboardService
{
    Task<DashboardStatsDto> GetTodayStatsAsync(CancellationToken ct = default);
    Task<DashboardQueueDto> GetQuickQueuesAsync(int top = 3, CancellationToken ct = default);
}

