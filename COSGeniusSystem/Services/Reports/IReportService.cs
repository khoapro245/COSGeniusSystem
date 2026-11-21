using System.Threading;
using System.Threading.Tasks;
using COSGeniusSystem.DTOs;

namespace COSGeniusSystem.Services;

public interface IReportService
{
    Task<StockImportReportDto> GetStockImportReportAsync(ReportFilterRequest filter, CancellationToken ct = default);
    Task<StockExportReportDto> GetStockExportReportAsync(ReportFilterRequest filter, CancellationToken ct = default);
}

