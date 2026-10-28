using Microsoft.AspNetCore.Mvc;
using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs.Invoices;
using COSGeniusSystem.Services.Interfaces;

namespace COSGeniusSystem.Controllers;

[ApiController]
[Route("api/v1/invoices")]
public class InvoicesController : ControllerBase
{
    private readonly IInvoiceService _invoiceService;

    public InvoicesController(IInvoiceService invoiceService)
    {
        _invoiceService = invoiceService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PaginatedResult<InvoiceListDto>>>> GetInvoices([FromQuery] InvoiceQueryParams queryParams)
    {
        var result = await _invoiceService.GetInvoicesAsync(queryParams);
        return Ok(ApiResponse<PaginatedResult<InvoiceListDto>>.Ok(result, "Lấy danh sách hóa đơn thành công."));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<InvoiceDetailDto>>> GetInvoiceById([FromRoute] int id)
    {
        var result = await _invoiceService.GetInvoiceByIdAsync(id);
        return Ok(ApiResponse<InvoiceDetailDto>.Ok(result, "Lấy chi tiết hóa đơn thành công."));
    }
}
