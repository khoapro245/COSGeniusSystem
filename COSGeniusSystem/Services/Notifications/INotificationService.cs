using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs;

namespace COSGeniusSystem.Services;

public interface INotificationService
{
    Task<PaginatedResult<NotificationDto>> GetNotificationsAsync(int userId, NotificationQueryParams queryParams);
    Task MarkAsReadAsync(int userId, int notificationId);
    Task MarkAllAsReadAsync(int userId);
}
