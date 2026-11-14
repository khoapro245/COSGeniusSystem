using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Services;
using Microsoft.AspNetCore.Mvc;

namespace COSGeniusSystem.Controllers;

[ApiController]
[Route("api/v1/disposals")]
public class DisposalsController : BaseApiController
{
    private readonly IDisposalService _disposalService;

    public DisposalsController(IDisposalService disposalService)
    {
        _disposalService = disposalService;
    }

    /// <summary>
    /// API-DISPOSAL-01: Lấy danh sách phiếu tiêu hủy từ Database.
    /// GET /api/v1/disposals
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponseModel<List<DisposalDto>>>> GetDisposals(CancellationToken ct = default)
    {
        var result = await _disposalService.GetDisposalsAsync(ct);
        return SuccessResponse(new SystemMessage(200, "Disposals retrieved successfully.", true), result);
    }

    /// <summary>
    /// API-DISPOSAL-02: Lấy chi tiết phiếu tiêu hủy theo ID hoặc mã (DisposalCode).
    /// GET /api/v1/disposals/{idOrCode}
    /// </summary>
    [HttpGet("{idOrCode}")]
    public async Task<ActionResult<ApiResponseModel<DisposalDto>>> GetDisposalByIdOrCode(string idOrCode, CancellationToken ct = default)
    {
        var result = await _disposalService.GetDisposalByIdOrCodeAsync(idOrCode, ct);
        if (result == null)
            throw new NotFoundException($"Disposal with identifier '{idOrCode}' was not found.");

        return SuccessResponse(new SystemMessage(200, "Disposal details retrieved successfully.", true), result);
    }

    /// <summary>
    /// API-DISPOSAL-03: Tạo mới đề xuất tiêu hủy vào Database (sinh từ kiểm kê hoặc thủ công).
    /// POST /api/v1/disposals
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponseModel<DisposalDto>>> CreateDisposal([FromBody] CreateDisposalRequest request, CancellationToken ct = default)
    {
        var result = await _disposalService.CreateDisposalAsync(request, ct);
        return SuccessResponse(new SystemMessage(201, "Disposal created successfully.", true), result);
    }

    /// <summary>
    /// API-DISPOSAL-04: Quản lý phê duyệt đề nghị tiêu hủy.
    /// PUT /api/v1/disposals/{idOrCode}/approve
    /// </summary>
    [HttpPut("{idOrCode}/approve")]
    public async Task<ActionResult<ApiResponseModel<DisposalDto>>> ApproveDisposal(string idOrCode, [FromBody] ApproveDisposalRequest request, CancellationToken ct = default)
    {
        var result = await _disposalService.ApproveDisposalAsync(idOrCode, request, ct);
        return SuccessResponse(new SystemMessage(200, "Disposal approved successfully.", true), result);
    }

    /// <summary>
    /// API-DISPOSAL-05: Quản lý từ chối đề nghị tiêu hủy.
    /// PUT /api/v1/disposals/{idOrCode}/reject
    /// </summary>
    [HttpPut("{idOrCode}/reject")]
    public async Task<ActionResult<ApiResponseModel<DisposalDto>>> RejectDisposal(string idOrCode, [FromBody] string? reason, CancellationToken ct = default)
    {
        var result = await _disposalService.RejectDisposalAsync(idOrCode, reason, ct);
        return SuccessResponse(new SystemMessage(200, "Disposal rejected successfully.", true), result);
    }

    /// <summary>
    /// API-DISPOSAL-06: Nhân viên kho xác nhận tiêu hủy thực tế (trừ tồn kho).
    /// PUT /api/v1/disposals/{idOrCode}/execute
    /// </summary>
    [HttpPut("{idOrCode}/execute")]
    public async Task<ActionResult<ApiResponseModel<DisposalDto>>> ExecuteDisposal(string idOrCode, [FromBody] ExecuteDisposalRequest request, CancellationToken ct = default)
    {
        var result = await _disposalService.ExecuteDisposalAsync(idOrCode, request, ct);
        return SuccessResponse(new SystemMessage(200, "Disposal executed and inventory reduced successfully.", true), result);
    }
}

