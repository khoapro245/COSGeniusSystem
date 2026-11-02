using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs;

namespace COSGeniusSystem.Services;

public interface IInvoiceService
{
    Task<PaginatedResult<InvoiceListDto>> GetInvoicesAsync(InvoiceQueryParams queryParams);
    Task<InvoiceDetailDto> GetInvoiceByIdAsync(int id);
}
