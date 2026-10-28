using Microsoft.EntityFrameworkCore;
using COSGeniusSystem.Commons;
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs.Notifications;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Services.Interfaces;

namespace COSGeniusSystem.Services;

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _context;

    public NotificationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedResult<NotificationDto>> GetNotificationsAsync(int userId, NotificationQueryParams queryParams)
    {
        var page = queryParams.Page <= 0 ? 1 : queryParams.Page;
        var pageSize = queryParams.PageSize <= 0 ? 20 : queryParams.PageSize;

        var query = _context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId);

        if (queryParams.IsRead.HasValue)
        {
            query = query.Where(n => n.IsRead == queryParams.IsRead.Value);
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationDto
            {
                NotificationId = n.NotificationId,
                UserId = n.UserId,
                Title = n.Title,
                Message = n.Message,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync();

        return new PaginatedResult<NotificationDto>(items, totalCount, page, pageSize);
    }

    public async Task MarkAsReadAsync(int userId, int notificationId)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.NotificationId == notificationId);

        if (notification == null)
        {
            throw new NotFoundException($"Không tìm thấy thông báo với ID: {notificationId}");
        }

        if (notification.UserId != userId)
        {
            throw new ForbiddenException("Bạn không có quyền thao tác trên thông báo này.");
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            await _context.SaveChangesAsync();
        }
    }

    public async Task MarkAllAsReadAsync(int userId)
    {
        await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(setter => setter.SetProperty(n => n.IsRead, true));
    }
}
