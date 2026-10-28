namespace COSGeniusSystem.DTOs.Notifications;

public class NotificationQueryParams
{
    public bool? IsRead { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
