using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs.Users;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services;
using COSGeniusSystem.Services.Users;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace COSGeniusSystem.UnitTests.Users;

public class UserServiceTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"ClinicHerbDb_User_{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static IPasswordHasher CreatePasswordHasher() => new PasswordHasher();

    [Fact]
    public async Task GetUsersAsync_FilterByRole_ReturnsMatchingUsers()
    {
        using var context = CreateInMemoryDbContext();
        var hasher = CreatePasswordHasher();

        var roleManager = new Role { RoleId = 1, RoleCode = "MANAGER", RoleName = "MANAGER" };
        var roleDoctor = new Role { RoleId = 2, RoleCode = "DOCTOR", RoleName = "DOCTOR" };
        context.Roles.AddRange(roleManager, roleDoctor);

        var u1 = new User { UserId = 1, Username = "manager1", FullName = "Nguyễn Văn Quản", PasswordHash = "hash", IsActive = true, CreatedAt = DateTime.UtcNow };
        u1.UserRoles.Add(new UserRole { UserId = 1, RoleId = 1, AssignedAt = DateTime.UtcNow });

        var u2 = new User { UserId = 2, Username = "doctor1", FullName = "Lê Bác Sĩ", PasswordHash = "hash", IsActive = true, CreatedAt = DateTime.UtcNow };
        u2.UserRoles.Add(new UserRole { UserId = 2, RoleId = 2, AssignedAt = DateTime.UtcNow });

        context.Users.AddRange(u1, u2);
        await context.SaveChangesAsync();

        var service = new UserService(context, hasher);
        var result = await service.GetUsersAsync(new UserFilterQuery { Role = "MANAGER" });

        Assert.Single(result);
        Assert.Equal("manager1", result[0].Username);
    }

    [Fact]
    public async Task GetUsersAsync_KeywordSearch_ReturnsMatchingUsers()
    {
        using var context = CreateInMemoryDbContext();
        var hasher = CreatePasswordHasher();

        var role = new Role { RoleId = 1, RoleCode = "INVENTORY_STAFF", RoleName = "INVENTORY_STAFF" };
        context.Roles.Add(role);

        var u1 = new User { UserId = 1, Username = "kho_binh", FullName = "Trần Bình Kho", Email = "binh@kho.vn", PasswordHash = "hash", IsActive = true, CreatedAt = DateTime.UtcNow };
        u1.UserRoles.Add(new UserRole { UserId = 1, RoleId = 1, AssignedAt = DateTime.UtcNow });

        var u2 = new User { UserId = 2, Username = "kho_an", FullName = "Phạm An Kho", Email = "an@kho.vn", PasswordHash = "hash", IsActive = true, CreatedAt = DateTime.UtcNow };
        u2.UserRoles.Add(new UserRole { UserId = 2, RoleId = 1, AssignedAt = DateTime.UtcNow });

        context.Users.AddRange(u1, u2);
        await context.SaveChangesAsync();

        var service = new UserService(context, hasher);
        var result = await service.GetUsersAsync(new UserFilterQuery { Keyword = "binh" });

        Assert.Single(result);
        Assert.Equal("kho_binh", result[0].Username);
    }

    [Fact]
    public async Task CreateUserAsync_ValidRequest_CreatesUserWithRole()
    {
        using var context = CreateInMemoryDbContext();
        var hasher = CreatePasswordHasher();

        context.Roles.Add(new Role { RoleId = 1, RoleCode = "PHARMACIST", RoleName = "PHARMACIST" });
        await context.SaveChangesAsync();

        var service = new UserService(context, hasher);
        var request = new CreateUserRequest
        {
            Username = "duocsi_moi",
            Password = "password123",
            FullName = "Nguyễn Thị Dược",
            Email = "duoc@clinic.vn",
            Phone = "0988776655",
            RoleCode = "PHARMACIST",
            IsActive = true
        };

        var (dto, error) = await service.CreateUserAsync(request);

        Assert.Null(error);
        Assert.NotNull(dto);
        Assert.Equal("duocsi_moi", dto.Username);
        Assert.Contains("PHARMACIST", dto.Roles);

        var savedUser = await context.Users.Include(u => u.UserRoles).FirstOrDefaultAsync(u => u.Username == "duocsi_moi");
        Assert.NotNull(savedUser);
        Assert.True(hasher.VerifyPassword("password123", savedUser.PasswordHash));
    }

    [Fact]
    public async Task CreateUserAsync_DuplicateUsername_ReturnsError()
    {
        using var context = CreateInMemoryDbContext();
        var hasher = CreatePasswordHasher();

        context.Users.Add(new User
        {
            UserId = 1,
            Username = "existing_user",
            FullName = "Người Cũ",
            PasswordHash = "hash",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = new UserService(context, hasher);
        var (dto, error) = await service.CreateUserAsync(new CreateUserRequest
        {
            Username = "existing_user",
            Password = "pass",
            FullName = "Người Trùng",
            RoleCode = "ADMIN"
        });

        Assert.Null(dto);
        Assert.NotNull(error);
        Assert.Contains("đã được sử dụng", error);
    }

    [Fact]
    public async Task UpdateUserAsync_ValidRequest_UpdatesFieldsAndRole()
    {
        using var context = CreateInMemoryDbContext();
        var hasher = CreatePasswordHasher();

        var role1 = new Role { RoleId = 1, RoleCode = "CASHIER", RoleName = "CASHIER" };
        var role2 = new Role { RoleId = 2, RoleCode = "MANAGER", RoleName = "MANAGER" };
        context.Roles.AddRange(role1, role2);

        var user = new User
        {
            UserId = 10,
            Username = "nhanvien1",
            FullName = "Tên Ban Đầu",
            PasswordHash = hasher.HashPassword("oldpass123"),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        user.UserRoles.Add(new UserRole { UserId = 10, RoleId = 1, AssignedAt = DateTime.UtcNow });
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var service = new UserService(context, hasher);
        var updateRequest = new UpdateUserRequest
        {
            FullName = "Tên Đã Đổi",
            Email = "newemail@clinic.vn",
            Phone = "0911223344",
            RoleCode = "MANAGER",
            IsActive = false,
            NewPassword = "newpassword456"
        };

        var (dto, error) = await service.UpdateUserAsync(10, updateRequest);

        Assert.Null(error);
        Assert.NotNull(dto);
        Assert.Equal("Tên Đã Đổi", dto.FullName);
        Assert.False(dto.IsActive);
        Assert.Contains("MANAGER", dto.Roles);

        var updatedInDb = await context.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).FirstAsync(u => u.UserId == 10);
        Assert.True(hasher.VerifyPassword("newpassword456", updatedInDb.PasswordHash));
    }

    [Fact]
    public async Task ToggleUserStatusAsync_DifferentUser_TogglesStatus()
    {
        using var context = CreateInMemoryDbContext();
        var hasher = CreatePasswordHasher();

        context.Users.Add(new User { UserId = 5, Username = "user5", FullName = "User 5", PasswordHash = "h", IsActive = true, CreatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var service = new UserService(context, hasher);
        var (success, isActive, error) = await service.ToggleUserStatusAsync(5, currentUserId: 1);

        Assert.True(success);
        Assert.False(isActive);
        Assert.Null(error);

        var userInDb = await context.Users.FindAsync(5);
        Assert.False(userInDb!.IsActive);
    }

    [Fact]
    public async Task ToggleUserStatusAsync_SameUser_PreventsSelfDeactivation()
    {
        using var context = CreateInMemoryDbContext();
        var hasher = CreatePasswordHasher();

        context.Users.Add(new User { UserId = 1, Username = "admin", FullName = "Admin", PasswordHash = "h", IsActive = true, CreatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var service = new UserService(context, hasher);
        var (success, isActive, error) = await service.ToggleUserStatusAsync(1, currentUserId: 1);

        Assert.False(success);
        Assert.True(isActive);
        Assert.NotNull(error);
        Assert.Contains("không thể tự vô hiệu hóa", error);
    }

    [Fact]
    public async Task GetRolesAsync_ReturnsAllRoles()
    {
        using var context = CreateInMemoryDbContext();
        var hasher = CreatePasswordHasher();

        context.Roles.AddRange(
            new Role { RoleId = 1, RoleCode = "ADMIN", RoleName = "ADMIN" },
            new Role { RoleId = 2, RoleCode = "DOCTOR", RoleName = "DOCTOR" }
        );
        await context.SaveChangesAsync();

        var service = new UserService(context, hasher);
        var roles = await service.GetRolesAsync();

        Assert.Equal(2, roles.Count);
    }
}

