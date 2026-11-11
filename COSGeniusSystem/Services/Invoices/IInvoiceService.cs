using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs;

namespace COSGeniusSystem.Services;

public interface IInvoiceService
{
    Task<PaginatedResult<InvoiceListDto>> GetInvoicesAsync(InvoiceQueryParams queryParams);
    Task<InvoiceDetailDto> GetInvoiceByIdAsync(int id);
    Task<InvoiceDetailDto> PayAsync(System.Security.Claims.ClaimsPrincipal actor, int id, InvoicePaymentRequest request);
    Task<InvoiceDetailDto> CancelAsync(System.Security.Claims.ClaimsPrincipal actor, int id, InvoiceCancelRequest request);
    Task<int> BackfillAsync();
}
