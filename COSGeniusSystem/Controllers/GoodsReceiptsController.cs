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

    /// <summary>
    /// API-GR-06: Lấy danh sách kệ kho gợi ý theo vị thuốc và thể tích/khối lượng cần nhập.
    /// GET /api/v1/goods-receipts/storage-locations
    /// </summary>
    [HttpGet("storage-locations")]
    [Authorize]
    public async Task<ActionResult<ApiResponseModel<List<StorageLocationSuggestionDto>>>> GetStorageLocations(
        [FromQuery] int? herbId,
        [FromQuery] decimal? requiredGrams,
        [FromQuery] int warehouseId = 1,
        CancellationToken ct = default)
    {
        var result = await _goodsReceiptService.GetSuggestedStorageLocationsAsync(herbId, requiredGrams, warehouseId, ct);
        return SuccessResponse(new SystemMessage(200, "Danh sách vị trí kệ kho gợi ý.", true), result);
    }

    /// <summary>
    /// API-GR-07: Tải lên chứng từ giao nhận (hình ảnh PNG/JPG hoặc tài liệu PDF) lưu trữ tại server.
    /// POST /api/v1/goods-receipts/upload-document
    /// </summary>
    [HttpPost("upload-document")]
    [Authorize]
    public async Task<ActionResult<ApiResponseModel<DocumentUploadResponseDto>>> UploadDocument(
        IFormFile file,
        [FromServices] IWebHostEnvironment env,
        CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
        {
            throw new BadRequestException("Vui lòng chọn file chứng từ để tải lên.");
        }

        // Giới hạn 10MB
        if (file.Length > 10 * 1024 * 1024)
        {
            throw new BadRequestException("Dung lượng file chứng từ không được vượt quá 10MB.");
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
        if (!allowedExtensions.Contains(ext))
        {
            throw new BadRequestException("Chỉ chấp nhận file chứng từ dạng hình ảnh (.jpg, .jpeg, .png) hoặc tài liệu PDF (.pdf).");
        }

        var webRoot = env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var uploadsFolder = Path.Combine(webRoot, "uploads", "receipts");
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        var uniqueFileName = $"doc_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}{ext}";
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream, ct);
        }

        var fileUrl = $"/uploads/receipts/{uniqueFileName}";
        var dto = new DocumentUploadResponseDto
        {
            FileUrl = fileUrl,
            FileName = file.FileName
        };

        return SuccessResponse(new SystemMessage(200, "Tải lên chứng từ thành công.", true), dto);
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                 ?? User.FindFirst("sub")?.Value
                 ?? User.FindFirst("id")?.Value;

        return int.TryParse(claim, out int id) ? id : 1;
    }
}

