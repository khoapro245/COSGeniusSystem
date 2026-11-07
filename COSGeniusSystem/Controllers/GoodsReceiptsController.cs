using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace COSGeniusSystem.Controllers;

[ApiController]
[Route("api/v1/goods-receipts")]
public class GoodsReceiptsController : BaseApiController
{
    private readonly IGoodsReceiptService _goodsReceiptService;

    public GoodsReceiptsController(IGoodsReceiptService goodsReceiptService)
    {
        _goodsReceiptService = goodsReceiptService;
    }

    /// <summary>
    /// API-GR-01: Lấy danh sách phiếu nhập kho.
    /// GET /api/v1/goods-receipts
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<ApiResponseModel<List<GoodsReceiptDto>>>> GetGoodsReceipts(
        [FromQuery] string? status,
        [FromQuery] int? poId,
        CancellationToken ct = default)
    {
        var result = await _goodsReceiptService.GetGoodsReceiptsAsync(status, poId, ct);
        return SuccessResponse(new SystemMessage(200, "Goods receipts retrieved successfully.", true), result);
    }

    /// <summary>
    /// API-GR-02: Lấy chi tiết phiếu nhập kho theo ID hoặc ReceiptCode.
    /// GET /api/v1/goods-receipts/{idOrCode}
    /// </summary>
    [HttpGet("{idOrCode}")]
    [Authorize]
    public async Task<ActionResult<ApiResponseModel<GoodsReceiptDto>>> GetGoodsReceiptDetail(
        string idOrCode,
        CancellationToken ct = default)
    {
        var result = await _goodsReceiptService.GetGoodsReceiptDetailAsync(idOrCode, ct);
        if (result == null)
        {
            throw new NotFoundException($"Goods receipt with identifier '{idOrCode}' was not found.");
        }

        return SuccessResponse(new SystemMessage(200, "Goods receipt details retrieved successfully.", true), result);
    }

    /// <summary>
    /// API-GR-03: Nhân viên kho tạo phiếu nhập kho liên kết PO (Hỗ trợ tách dòng lô hàng).
    /// POST /api/v1/goods-receipts
    /// </summary>
    [HttpPost]
    [Authorize(Roles = $"{RoleConstants.InventoryStaff},{RoleConstants.Manager},{RoleConstants.Admin}")]
    public async Task<ActionResult<ApiResponseModel<GoodsReceiptDto>>> CreateGoodsReceipt(
        [FromBody] CreateGoodsReceiptRequest request,
        CancellationToken ct = default)
    {
        int userId = GetCurrentUserId();
        var result = await _goodsReceiptService.CreateGoodsReceiptAsync(userId, request, ct);
        return SuccessResponse(new SystemMessage(201, "Phiếu nhập kho đã được tạo thành công ở trạng thái chờ duyệt.", true), result);
    }

    /// <summary>
    /// API-GR-04: Gửi phiếu nhập kho cho Quản lý duyệt.
    /// POST /api/v1/goods-receipts/{id}/submit
    /// </summary>
    [HttpPost("{id}/submit")]
    [Authorize(Roles = $"{RoleConstants.InventoryStaff},{RoleConstants.Manager},{RoleConstants.Admin}")]
    public async Task<ActionResult<ApiResponseModel<GoodsReceiptDto>>> SubmitForApproval(
        int id,
        CancellationToken ct = default)
    {
        var result = await _goodsReceiptService.SubmitForApprovalAsync(id, ct);
        return SuccessResponse(new SystemMessage(200, "Phiếu nhập kho đã được gửi cho Quản lý duyệt.", true), result);
    }

    /// <summary>
    /// API-GR-05: Quản lý phê duyệt (Putaway: cộng tồn kho theo từng lô) hoặc từ chối phiếu nhập kho.
    /// POST /api/v1/goods-receipts/{id}/approve
    /// </summary>
    [HttpPost("{id}/approve")]
    [Authorize(Roles = $"{RoleConstants.Manager},{RoleConstants.Admin}")]
    public async Task<ActionResult<ApiResponseModel<GoodsReceiptDto>>> ApproveGoodsReceipt(
        int id,
        [FromBody] ApproveGoodsReceiptRequest request,
        CancellationToken ct = default)
    {
        int managerId = GetCurrentUserId();
        var result = await _goodsReceiptService.ApproveGoodsReceiptAsync(managerId, id, request, ct);
        string message = request.IsApproved
            ? "Đã phê duyệt nhập kho thành công! Đã tăng số lượng tồn kho theo từng lô hàng."
            : "Đã từ chối phê duyệt phiếu nhập kho.";
        return SuccessResponse(new SystemMessage(200, message, true), result);
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                 ?? User.FindFirst("sub")?.Value
                 ?? User.FindFirst("id")?.Value;

        return int.TryParse(claim, out int id) ? id : 1;
    }
}

