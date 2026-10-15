namespace COSGeniusSystem.Model;

public class Notification
{
    public Guid NotificationId { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public bool IsRead { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public User User { get; set; } = null!;
}
