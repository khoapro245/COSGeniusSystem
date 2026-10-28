using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs.Notifications;

namespace COSGeniusSystem.Services.Interfaces;

public interface INotificationService
{
    Task<PaginatedResult<NotificationDto>> GetNotificationsAsync(int userId, NotificationQueryParams queryParams);
    Task MarkAsReadAsync(int userId, int notificationId);
    Task MarkAllAsReadAsync(int userId);
}
