using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs.Reception;
using COSGeniusSystem.Services.Reception;
using Microsoft.AspNetCore.Mvc;

namespace COSGeniusSystem.Controllers;

[ApiController]
[Route("api/v1/reception/dashboard")]
public sealed class ReceptionDashboardController : BaseApiController
{
    private readonly IReceptionDashboardService _dashboardService;

    public ReceptionDashboardController(IReceptionDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    /// <summary>
    /// Lấy dữ liệu thống kê tổng quan tiếp tân trong ngày.
    /// GET /api/v1/reception/dashboard/stats
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult<ApiResponseModel<DashboardStatsDto>>> GetStats(CancellationToken ct = default)
    {
        var result = await _dashboardService.GetTodayStatsAsync(ct);
        return SuccessResponse(SystemMessages.DashboardStatsRetrieved, result);
    }

    /// <summary>
    /// Lấy danh sách 2 hàng đợi thu gọn (chờ khám & chờ trị liệu) hiển thị trang chủ.
    /// GET /api/v1/reception/dashboard/quick-queues
    /// </summary>
    [HttpGet("quick-queues")]
    public async Task<ActionResult<ApiResponseModel<DashboardQueueDto>>> GetQuickQueues(
        [FromQuery] int top = 3, CancellationToken ct = default)
    {
        var result = await _dashboardService.GetQuickQueuesAsync(top, ct);
        return SuccessResponse(SystemMessages.DashboardQuickQueuesRetrieved, result);
    }
}

