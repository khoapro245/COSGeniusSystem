using Microsoft.AspNetCore.Authorization;
using COSGeniusSystem.Commons;
using COSGeniusSystem.Services;
using Microsoft.AspNetCore.Mvc;
using COSGeniusSystem.DTOs;

namespace COSGeniusSystem.Controllers;

[Authorize(Roles = "CASHIER,ADMIN")]
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
    public async Task<ActionResult<ApiResponseModel<PaginatedResult<InvoiceListDto>>>> GetInvoices([FromQuery] InvoiceQueryParams queryParams)
    {
        var result = await _invoiceService.GetInvoicesAsync(queryParams);
        return Ok(new ApiResponseModel<PaginatedResult<InvoiceListDto>>(200, "Thành công.", true, result));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponseModel<InvoiceDetailDto>>> GetInvoiceById([FromRoute] int id)
    {
        var result = await _invoiceService.GetInvoiceByIdAsync(id);
        return Ok(new ApiResponseModel<InvoiceDetailDto>(200, "Thành công.", true, result));
    }
    [HttpGet("payment-methods")]
    public IActionResult Methods() => Ok(new ApiResponseModel<string[]>(200, "Thành công.", true, InvoiceService.PaymentMethods));
    [HttpPost("{id:int}/pay")]
    public async Task<IActionResult> Pay(int id, InvoicePaymentRequest request) => Ok(new ApiResponseModel<InvoiceDetailDto>(200, "Đã nhận đủ tiền.", true, await _invoiceService.PayAsync(User, id, request)));
    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, InvoiceCancelRequest request) => Ok(new ApiResponseModel<InvoiceDetailDto>(200, "Đã hủy hóa đơn; khoản đủ điều kiện được lập lại, giữ lịch sử.", true, await _invoiceService.CancelAsync(User, id, request)));
    [Authorize(Roles = "ADMIN")]
    [HttpPost("backfill")]
    public async Task<IActionResult> Backfill() => Ok(new ApiResponseModel<int>(200, "Đã rà soát hồ sơ và lập bù khoản đủ điều kiện.", true, await _invoiceService.BackfillAsync()));
}
