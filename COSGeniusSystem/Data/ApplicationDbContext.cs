using Microsoft.EntityFrameworkCore;
using COSGeniusSystem.Model;

namespace COSGeniusSystem.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ==========================================
        // 1. Role Configuration
        // ==========================================
        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles");

            entity.HasKey(r => r.RoleId);

            entity.Property(r => r.RoleId)
                .HasColumnName("role_id");

            entity.Property(r => r.RoleName)
                .HasColumnName("role_name")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(r => r.Description)
                .HasColumnName("description")
                .HasMaxLength(250);

            entity.Property(r => r.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(r => r.RoleName)
                .IsUnique();

            // Seed default roles
            entity.HasData(
                new Role
                {
                    RoleId = new Guid("11111111-1111-1111-1111-111111111111"),
                    RoleName = "Admin",
                    Description = "System Administrator",
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Role
                {
                    RoleId = new Guid("22222222-2222-2222-2222-222222222222"),
                    RoleName = "User",
                    Description = "Standard System User",
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            );
        });

        // ==========================================
        // 2. User Configuration
        // ==========================================
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");

            entity.HasKey(u => u.UserId);

            entity.Property(u => u.UserId)
                .HasColumnName("user_id");

            entity.Property(u => u.Username)
                .HasColumnName("username")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(u => u.PasswordHash)
                .HasColumnName("password_hash")
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(u => u.FullName)
                .HasColumnName("full_name")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(u => u.Email)
                .HasColumnName("email")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(u => u.Phone)
                .HasColumnName("phone")
                .HasMaxLength(20);

            entity.Property(u => u.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true);

            entity.Property(u => u.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(u => u.UpdatedAt)
                .HasColumnName("updated_at");

            entity.Property(u => u.LastLoginAt)
                .HasColumnName("last_login_at");

            entity.HasIndex(u => u.Username)
                .IsUnique();

            entity.HasIndex(u => u.Email)
                .IsUnique();
        });

        // ==========================================
        // 3. UserRole Configuration (Many-to-Many join)
        // ==========================================
        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("UserRoles");

            entity.HasKey(ur => new { ur.UserId, ur.RoleId });

            entity.Property(ur => ur.UserId)
                .HasColumnName("user_id");

            entity.Property(ur => ur.RoleId)
                .HasColumnName("role_id");

            entity.Property(ur => ur.AssignedAt)
                .HasColumnName("assigned_at")
                .HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ==========================================
        // 4. Notification Configuration
        // ==========================================
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("Notifications");

            entity.HasKey(n => n.NotificationId);

            entity.Property(n => n.NotificationId)
                .HasColumnName("notification_id");

            entity.Property(n => n.UserId)
                .HasColumnName("user_id");

            entity.Property(n => n.Title)
                .HasColumnName("title")
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(n => n.Content)
                .HasColumnName("content")
                .HasMaxLength(1000)
                .IsRequired();

            entity.Property(n => n.IsRead)
                .HasColumnName("is_read")
                .HasDefaultValue(false);

            entity.Property(n => n.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(n => n.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
