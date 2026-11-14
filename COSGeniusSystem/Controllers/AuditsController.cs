using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace COSGeniusSystem.Controllers;

[ApiController]
[Route("api/v1/audits")]
public class AuditsController : BaseApiController
{
    private readonly IAuditService _auditService;

    public AuditsController(IAuditService auditService)
    {
        _auditService = auditService;
    }

    /// <summary>
    /// API-AUDIT-01: Lấy danh sách phiếu kiểm kê.
    /// GET /api/v1/audits
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponseModel<List<InventoryAuditDto>>>> GetAudits(CancellationToken ct = default)
    {
        var result = await _auditService.GetAuditsAsync(ct);
        return SuccessResponse(new SystemMessage(200, "Audits retrieved successfully.", true), result);
    }

    /// <summary>
    /// API-AUDIT-02: Lấy chi tiết phiếu kiểm kê theo ID hoặc mã (AuditCode).
    /// GET /api/v1/audits/{idOrCode}
    /// </summary>
    [HttpGet("{idOrCode}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponseModel<InventoryAuditDto>>> GetAuditByIdOrCode(string idOrCode, CancellationToken ct = default)
    {
        var result = await _auditService.GetAuditByIdOrCodeAsync(idOrCode, ct);
        if (result == null)
            throw new NotFoundException($"Audit with identifier '{idOrCode}' was not found.");

        return SuccessResponse(new SystemMessage(200, "Audit details retrieved successfully.", true), result);
    }

    /// <summary>
    /// API-AUDIT-03: Quản lý tạo / lập lịch phiếu kiểm kê cho nhân viên kho.
    /// POST /api/v1/audits
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponseModel<InventoryAuditDto>>> CreateAudit([FromBody] CreateAuditRequest request, CancellationToken ct = default)
    {
        int tokenUserId = 0;
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(claim, out int uid))
        {
            tokenUserId = uid;
        }

        var result = await _auditService.CreateAuditAsync(request, tokenUserId, ct);
        return SuccessResponse(new SystemMessage(201, "Phiếu kiểm kê đã được tạo thành công.", true), result);
    }

    /// <summary>
    /// API-AUDIT-06: Cập nhật trạng thái phiếu kiểm kê (Duyệt lịch, Bắt đầu kiểm kê...).
    /// PUT /api/v1/audits/{idOrCode}/status
    /// </summary>
    [HttpPut("{idOrCode}/status")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponseModel<InventoryAuditDto>>> UpdateAuditStatus(
        string idOrCode,
        [FromQuery] string newStatus,
        [FromQuery] string? note = null,
        CancellationToken ct = default)
    {
        var result = await _auditService.UpdateAuditStatusAsync(idOrCode, newStatus, note, ct);
        return SuccessResponse(new SystemMessage(200, $"Đã cập nhật trạng thái phiếu sang {newStatus}.", true), result);
    }

    /// <summary>
    /// API-AUDIT-04: Nhân viên kho cập nhật số kiểm đếm / Hoàn tất kiểm kê.
    /// PUT /api/v1/audits/{idOrCode}/count
    /// </summary>
    [HttpPut("{idOrCode}/count")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponseModel<InventoryAuditDto>>> UpdateAuditCount(string idOrCode, [FromBody] UpdateAuditCountRequest request, CancellationToken ct = default)
    {
        var result = await _auditService.UpdateAuditCountAsync(idOrCode, request, ct);
        string msg = request.IsComplete ? "Đã hoàn tất kiểm kê và chuyển phiếu sang Quản lý duyệt." : "Đã lưu tiến độ kiểm đếm.";
        return SuccessResponse(new SystemMessage(200, msg, true), result);
    }

    /// <summary>
    /// API-AUDIT-05: Quản lý duyệt điều chỉnh tồn kho theo số thực tế và đồng bộ tồn kho.
    /// POST /api/v1/audits/{idOrCode}/adjust
    /// </summary>
    [HttpPost("{idOrCode}/adjust")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponseModel<InventoryAuditDto>>> ApproveAuditAdjustment(string idOrCode, [FromBody] ApproveAuditRequest request, CancellationToken ct = default)
    {
        var result = await _auditService.ApproveAuditAdjustmentAsync(idOrCode, request, ct);
        return SuccessResponse(new SystemMessage(200, "Đã duyệt điều chỉnh tồn kho thành công và đồng bộ dữ liệu kho.", true), result);
    }
}

