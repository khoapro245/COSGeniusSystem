namespace COSGeniusSystem.Model;

public class UserRole
{
    public Guid UserId { get; set; }

    public Guid RoleId { get; set; }

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public User User { get; set; } = null!;

    public Role Role { get; set; } = null!;
}
