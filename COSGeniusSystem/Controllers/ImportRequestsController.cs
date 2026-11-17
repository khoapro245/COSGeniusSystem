// [hieppm] Start: Controller cho luồng Đề xuất & Yêu cầu bổ sung kho (ImportRequest)
using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace COSGeniusSystem.Controllers;

[ApiController]
[Route("api/v1/import-requests")]
public class ImportRequestsController : BaseApiController
{
    private readonly IImportRequestService _importRequestService;

    public ImportRequestsController(IImportRequestService importRequestService)
    {
        _importRequestService = importRequestService;
    }

    /// <summary>
    /// API-IR-01: Lấy danh sách yêu cầu bổ sung kho.
    /// GET /api/v1/import-requests
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<ApiResponseModel<List<ImportRequestDto>>>> GetImportRequests(
        [FromQuery] string? status,
        CancellationToken ct = default)
    {
        var result = await _importRequestService.GetImportRequestsAsync(status, ct);
        return SuccessResponse(new SystemMessage(200, "Danh sách yêu cầu bổ sung kho tải thành công.", true), result);
    }

    /// <summary>
    /// API-IR-02: Lấy chi tiết yêu cầu bổ sung kho theo ID hoặc RequestCode.
    /// GET /api/v1/import-requests/{idOrCode}
    /// </summary>
    [HttpGet("{idOrCode}")]
    [Authorize]
    public async Task<ActionResult<ApiResponseModel<ImportRequestDto>>> GetImportRequestDetail(
        string idOrCode,
        CancellationToken ct = default)
    {
        var result = await _importRequestService.GetImportRequestDetailAsync(idOrCode, ct);
        if (result == null)
        {
            throw new NotFoundException($"Không tìm thấy yêu cầu bổ sung kho '{idOrCode}'.");
        }

        return SuccessResponse(new SystemMessage(200, "Chi tiết yêu cầu bổ sung kho tải thành công.", true), result);
    }

    /// <summary>
    /// API-IR-03: Nhân viên kho tạo đề xuất bổ sung kho mới.
    /// POST /api/v1/import-requests
    /// </summary>
    [HttpPost]
    [Authorize(Roles = $"{RoleConstants.InventoryStaff},{RoleConstants.Admin},{RoleConstants.Manager}")]
    public async Task<ActionResult<ApiResponseModel<ImportRequestDto>>> CreateImportRequest(
        [FromBody] CreateImportRequestRequest request,
        CancellationToken ct = default)
    {
        int userId = GetCurrentUserId();
        var result = await _importRequestService.CreateImportRequestAsync(userId, request, ct);
        return SuccessResponse(new SystemMessage(201, "Yêu cầu bổ sung kho đã được tạo thành công.", true), result);
    }

    /// <summary>
    /// API-IR-04: Quản lý phê duyệt hoặc từ chối yêu cầu bổ sung kho.
    /// PUT /api/v1/import-requests/{id}/review
    /// </summary>
    [HttpPut("{id}/review")]
    [Authorize(Roles = $"{RoleConstants.Manager},{RoleConstants.Admin}")]
    public async Task<ActionResult<ApiResponseModel<ImportRequestDto>>> ReviewImportRequest(
        int id,
        [FromBody] ReviewImportRequestRequest request,
        CancellationToken ct = default)
    {
        int managerId = GetCurrentUserId();
        var result = await _importRequestService.ReviewImportRequestAsync(managerId, id, request, ct);
        var msg = request.IsApproved
            ? "Yêu cầu bổ sung kho đã được phê duyệt thành công. Có thể tiến hành lập đơn PO."
            : "Yêu cầu bổ sung kho đã bị từ chối.";
        return SuccessResponse(new SystemMessage(200, msg, true), result);
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                 ?? User.FindFirst("sub")?.Value
                 ?? User.FindFirst("id")?.Value;

        return int.TryParse(claim, out int id) ? id : 1;
    }
}
// [hieppm] End: Controller cho luồng Đề xuất & Yêu cầu bổ sung kho
