using System.Threading;
using System.Threading.Tasks;
using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace COSGeniusSystem.Controllers;

[ApiController]
[Route("api/v1/reports")]
[Route("api/reports")]
[Authorize]
public class ReportsController : BaseApiController
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    /// <summary>
    /// API Báo cáo nhập kho:
    /// Thống kê tổng số đơn nhập, danh sách vị thuốc & nhà cung cấp tương ứng, tổng chi phí tốn cho nhập kho.
    /// GET /api/v1/reports/import-summary
    /// </summary>
    [HttpGet("import-summary")]
    public async Task<ActionResult<ApiResponseModel<StockImportReportDto>>> GetStockImportReport(
        [FromQuery] ReportFilterRequest filter,
        CancellationToken ct = default)
    {
        var result = await _reportService.GetStockImportReportAsync(filter, ct);
        return SuccessResponse(new SystemMessage(200, "Lấy báo cáo nhập kho thành công.", true), result);
    }

    /// <summary>
    /// API Báo cáo xuất kho:
    /// Thống kê số đơn thuốc bán ra, số đơn vị thuốc xuất bán, tổng doanh thu thu được, lãi thực tế.
    /// GET /api/v1/reports/export-summary
    /// </summary>
    [HttpGet("export-summary")]
    public async Task<ActionResult<ApiResponseModel<StockExportReportDto>>> GetStockExportReport(
        [FromQuery] ReportFilterRequest filter,
        CancellationToken ct = default)
    {
        var result = await _reportService.GetStockExportReportAsync(filter, ct);
        return SuccessResponse(new SystemMessage(200, "Lấy báo cáo xuất kho thành công.", true), result);
    }
}

