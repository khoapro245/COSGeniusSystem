using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace COSGeniusSystem.Controllers;

[ApiController]
[Route("api/v1/supplier-returns")]
public class SupplierReturnsController : BaseApiController
{
    private readonly ISupplierReturnService _supplierReturnService;

    public SupplierReturnsController(ISupplierReturnService supplierReturnService)
    {
        _supplierReturnService = supplierReturnService;
    }

    /// <summary>
    /// API-SR-01: Lấy danh sách phiếu trả nhà cung cấp từ CSDL.
    /// GET /api/v1/supplier-returns
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponseModel<List<SupplierReturnDto>>>> GetSupplierReturns(CancellationToken ct = default)
    {
        var result = await _supplierReturnService.GetSupplierReturnsAsync(ct);
        return SuccessResponse(new SystemMessage(200, "Danh sách phiếu trả nhà cung cấp đã được tải từ CSDL.", true), result);
    }

    /// <summary>
    /// API-SR-02: Lấy chi tiết phiếu trả theo ID hoặc mã (ReturnCode, VD: PT00003).
    /// GET /api/v1/supplier-returns/{idOrCode}
    /// </summary>
    [HttpGet("{idOrCode}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponseModel<SupplierReturnDto>>> GetSupplierReturnByIdOrCode(string idOrCode, CancellationToken ct = default)
    {
        var result = await _supplierReturnService.GetSupplierReturnByIdOrCodeAsync(idOrCode, ct);
        if (result == null)
            throw new NotFoundException($"Không tìm thấy phiếu trả nhà cung cấp '{idOrCode}'.");

        return SuccessResponse(new SystemMessage(200, "Chi tiết phiếu trả nhà cung cấp.", true), result);
    }

    /// <summary>
    /// API-SR-03: Tạo mới đề xuất trả nhà cung cấp vào Database.
    /// POST /api/v1/supplier-returns
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponseModel<SupplierReturnDto>>> CreateSupplierReturn([FromBody] CreateSupplierReturnRequest request, CancellationToken ct = default)
    {
        int userId = GetCurrentUserId();
        var result = await _supplierReturnService.CreateSupplierReturnAsync(userId, request, ct);
        return SuccessResponse(new SystemMessage(201, "Đã tạo đề nghị trả nhà cung cấp thành công.", true), result);
    }

    /// <summary>
    /// API-SR-04: Quản lý phê duyệt đề nghị trả nhà cung cấp.
    /// PUT /api/v1/supplier-returns/{idOrCode}/approve
    /// </summary>
    [HttpPut("{idOrCode}/approve")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponseModel<SupplierReturnDto>>> ApproveSupplierReturn(string idOrCode, [FromBody] ApproveSupplierReturnRequest request, CancellationToken ct = default)
    {
        int userId = GetCurrentUserId();
        var result = await _supplierReturnService.ApproveSupplierReturnAsync(userId, idOrCode, request, ct);
        return SuccessResponse(new SystemMessage(200, "Đã phê duyệt đề nghị trả nhà cung cấp thành công.", true), result);
    }

    /// <summary>
    /// API-SR-05: Quản lý từ chối đề nghị trả nhà cung cấp.
    /// PUT /api/v1/supplier-returns/{idOrCode}/reject
    /// </summary>
    [HttpPut("{idOrCode}/reject")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponseModel<SupplierReturnDto>>> RejectSupplierReturn(string idOrCode, [FromBody] RejectSupplierReturnRequest request, CancellationToken ct = default)
    {
        int userId = GetCurrentUserId();
        var result = await _supplierReturnService.RejectSupplierReturnAsync(userId, idOrCode, request, ct);
        return SuccessResponse(new SystemMessage(200, "Đã từ chối đề nghị trả nhà cung cấp.", true), result);
    }

    /// <summary>
    /// API-SR-06: Thủ kho xác nhận bàn giao thực tế và xuất trả (trừ tồn kho CSDL).
    /// PUT /api/v1/supplier-returns/{idOrCode}/handover
    /// </summary>
    [HttpPut("{idOrCode}/handover")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponseModel<SupplierReturnDto>>> HandoverSupplierReturn(string idOrCode, [FromBody] HandoverSupplierReturnRequest request, CancellationToken ct = default)
    {
        int userId = GetCurrentUserId();
        var result = await _supplierReturnService.HandoverSupplierReturnAsync(userId, idOrCode, request, ct);
        return SuccessResponse(new SystemMessage(200, "Đã xác nhận xuất trả và ghi giảm tồn kho thực tế trong CSDL.", true), result);
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                 ?? User.FindFirst("sub")?.Value
                 ?? User.FindFirst("id")?.Value;

        return int.TryParse(claim, out int id) ? id : 1;
    }
}
