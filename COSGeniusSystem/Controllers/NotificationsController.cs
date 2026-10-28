using Microsoft.AspNetCore.Mvc;
using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs.Notifications;
using COSGeniusSystem.Services.Interfaces;

namespace COSGeniusSystem.Controllers;

[ApiController]
[Route("api/v1/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    private int? GetCurrentUserId(int? queryUserId, int? headerUserId)
    {
        if (queryUserId.HasValue && queryUserId.Value > 0)
        {
            return queryUserId.Value;
        }

        if (headerUserId.HasValue && headerUserId.Value > 0)
        {
            return headerUserId.Value;
        }

        return null;
    }

    /// <summary>
    /// Lấy danh sách thông báo của người dùng (hỗ trợ lọc đã đọc/chưa đọc và phân trang)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PaginatedResult<NotificationDto>>>> GetNotifications(
        [FromQuery] int? userId,
        [FromHeader(Name = "X-User-Id")] int? headerUserId,
        [FromQuery] NotificationQueryParams queryParams)
    {
        var resolvedUserId = GetCurrentUserId(userId, headerUserId);
        if (resolvedUserId == null)
        {
            return BadRequest(ApiResponse.Fail("Vui lòng nhập 'userId' (qua ô query param 'userId' hoặc header 'X-User-Id')."));
        }

        var result = await _notificationService.GetNotificationsAsync(resolvedUserId.Value, queryParams);
        return Ok(ApiResponse<PaginatedResult<NotificationDto>>.Ok(result, "Lấy danh sách thông báo thành công."));
    }

    /// <summary>
    /// Đánh dấu một thông báo là đã đọc
    /// </summary>
    [HttpPut("{id:int}/read")]
    public async Task<ActionResult<ApiResponse>> MarkAsRead(
        [FromRoute] int id,
        [FromQuery] int? userId,
        [FromHeader(Name = "X-User-Id")] int? headerUserId)
    {
        var resolvedUserId = GetCurrentUserId(userId, headerUserId);
        if (resolvedUserId == null)
        {
            return BadRequest(ApiResponse.Fail("Vui lòng nhập 'userId' (qua ô query param 'userId' hoặc header 'X-User-Id')."));
        }

        await _notificationService.MarkAsReadAsync(resolvedUserId.Value, id);
        return Ok(ApiResponse.Ok("Đã đánh dấu thông báo đã đọc thành công."));
    }

    /// <summary>
    /// Đánh dấu tất cả thông báo chưa đọc của người dùng thành đã đọc
    /// </summary>
    [HttpPut("read-all")]
    public async Task<ActionResult<ApiResponse>> MarkAllAsRead(
        [FromQuery] int? userId,
        [FromHeader(Name = "X-User-Id")] int? headerUserId)
    {
        var resolvedUserId = GetCurrentUserId(userId, headerUserId);
        if (resolvedUserId == null)
        {
            return BadRequest(ApiResponse.Fail("Vui lòng nhập 'userId' (qua ô query param 'userId' hoặc header 'X-User-Id')."));
        }

        await _notificationService.MarkAllAsReadAsync(resolvedUserId.Value);
        return Ok(ApiResponse.Ok("Đã đánh dấu tất cả thông báo là đã đọc thành công."));
    }
}
