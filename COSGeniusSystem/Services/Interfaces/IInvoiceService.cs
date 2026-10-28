using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs.Invoices;

namespace COSGeniusSystem.Services.Interfaces;

public interface IInvoiceService
{
    Task<PaginatedResult<InvoiceListDto>> GetInvoicesAsync(InvoiceQueryParams queryParams);
    Task<InvoiceDetailDto> GetInvoiceByIdAsync(int id);
}
