using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace COSGeniusSystem.Controllers;

[ApiController]
[Route("api/v1/purchase-orders")]
public class PurchaseOrdersController : BaseApiController
{
    private readonly IPurchaseOrderService _purchaseOrderService;

    public PurchaseOrdersController(IPurchaseOrderService purchaseOrderService)
    {
        _purchaseOrderService = purchaseOrderService;
    }

    /// <summary>
    /// API-PO-01: Lấy danh sách đơn mua hàng (PO).
    /// GET /api/v1/purchase-orders
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<ApiResponseModel<List<PurchaseOrderDto>>>> GetPurchaseOrders(
        [FromQuery] string? status,
        [FromQuery] string? search,
        CancellationToken ct = default)
    {
        var result = await _purchaseOrderService.GetPurchaseOrdersAsync(status, search, ct);
        return SuccessResponse(new SystemMessage(200, "Purchase orders retrieved successfully.", true), result);
    }

    /// <summary>
    /// API-PO-02: Lấy chi tiết đơn mua hàng theo ID hoặc POCode (Bao gồm bảng đối chiếu 3 cột).
    /// GET /api/v1/purchase-orders/{idOrCode}
    /// </summary>
    [HttpGet("{idOrCode}")]
    [Authorize]
    public async Task<ActionResult<ApiResponseModel<PurchaseOrderDto>>> GetPurchaseOrderDetail(
        string idOrCode,
        CancellationToken ct = default)
    {
        var result = await _purchaseOrderService.GetPurchaseOrderDetailAsync(idOrCode, ct);
        if (result == null)
        {
            throw new NotFoundException($"Purchase order with identifier '{idOrCode}' was not found.");
        }

        return SuccessResponse(new SystemMessage(200, "Purchase order details retrieved successfully.", true), result);
    }

    /// <summary>
    /// API-PO-03: Quản lý tạo đơn mua hàng mới.
    /// POST /api/v1/purchase-orders
    /// </summary>
    [HttpPost]
    [Authorize(Roles = $"{RoleConstants.Manager},{RoleConstants.Admin}")]
    public async Task<ActionResult<ApiResponseModel<PurchaseOrderDto>>> CreatePurchaseOrder(
        [FromBody] CreatePORequest request,
        CancellationToken ct = default)
    {
        int userId = GetCurrentUserId();
        var result = await _purchaseOrderService.CreatePOAsync(userId, request, ct);
        return SuccessResponse(new SystemMessage(201, "Đơn mua hàng đã được tạo thành công.", true), result);
    }

    /// <summary>
    /// API-PO-04: Quản lý cập nhật đơn mua hàng (Chỉ cho phép khi ở trạng thái 'Nháp').
    /// PUT /api/v1/purchase-orders/{id}
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = $"{RoleConstants.Manager},{RoleConstants.Admin}")]
    public async Task<ActionResult<ApiResponseModel<PurchaseOrderDto>>> UpdatePurchaseOrder(
        int id,
        [FromBody] UpdatePORequest request,
        CancellationToken ct = default)
    {
        var result = await _purchaseOrderService.UpdatePOAsync(id, request, ct);
        return SuccessResponse(new SystemMessage(200, "Cập nhật đơn mua hàng thành công.", true), result);
    }

    /// <summary>
    /// API-PO-05: Quản lý xóa đơn mua hàng (Chỉ cho phép khi ở trạng thái 'Nháp').
    /// DELETE /api/v1/purchase-orders/{id}
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = $"{RoleConstants.Manager},{RoleConstants.Admin}")]
    public async Task<ActionResult<ApiResponseModel<object?>>> DeletePurchaseOrder(
        int id,
        CancellationToken ct = default)
    {
        await _purchaseOrderService.DeletePOAsync(id, ct);
        return SuccessResponse(new SystemMessage(200, "Đã xóa đơn mua hàng nháp thành công.", true));
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                 ?? User.FindFirst("sub")?.Value
                 ?? User.FindFirst("id")?.Value;

        return int.TryParse(claim, out int id) ? id : 1;
    }
}

