namespace COSGeniusSystem.Model;

public class Role
{
    public Guid RoleId { get; set; } = Guid.NewGuid();

    public string RoleName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
