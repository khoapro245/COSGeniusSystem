using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

// Assumes ApplicationDbContext accepts DbContextOptions<ApplicationDbContext>
// and GenerateToken accepts List<string> (adjust mock signature to interface if needed).
public class AuthServiceTests : IDisposable
{
    private readonly ApplicationDbContext db;
    private readonly Mock<IPasswordHasher> hasher = new();
    private readonly Mock<ITokenService> tokens = new();
    private readonly Mock<ITokenBlacklistService> blacklist = new();
    private readonly AuthService service;
    public AuthServiceTests()
    {
        db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        hasher.Setup(x => x.HashPassword(It.IsAny<string>())).Returns("new_hash");
        hasher.Setup(x => x.VerifyPassword("Test@123", "old_hash")).Returns(true);
        tokens.Setup(x => x.GenerateToken(It.IsAny<User>(), It.IsAny<List<string>>()))
            .Returns(("valid_token", 3600));
        service = new AuthService(db, hasher.Object, tokens.Object, blacklist.Object, NullLogger<AuthService>.Instance);
    }
    private async Task<User> Seed(bool active = true)
    {
        var role = new Role { RoleCode = "DOCTOR", RoleName = "Doctor", Description = "Doctor" };
        var user = new User { UserId = 1, Username = "doctor01", FullName = "Test User", Email = "doctor@test.com", PasswordHash = "old_hash", IsActive = active, CreatedAt = DateTime.UtcNow };
        user.UserRoles.Add(new UserRole { Role = role, AssignedAt = DateTime.UtcNow });
        db.Users.Add(user); await db.SaveChangesAsync(); return user;
    }
    private static RegisterRequestDto Registration(string username = "newdoctor", string email = "new@test.com", string role = "DOCTOR") => new()
    { Username = username, Email = email, RoleCode = role, Password = "Test@123", FullName = "New Doctor" };

    // UTC-AUTH-001
    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsToken()
    {
        await Seed();
        var result = await service.LoginAsync(new LoginRequestDto
        { Username = "doctor01", Password = "Test@123" });
        Assert.Equal("valid_token", result.AccessToken);
        Assert.Equal("Bearer", result.TokenType);
        Assert.Equal(3600, result.ExpiresIn);
        Assert.Equal("doctor01", result.User.Username);
        Assert.Contains("DOCTOR", result.User.Roles);
    }

    // UTC-AUTH-002
    [Fact]
    public async Task LoginAsync_WrongPassword_ThrowsUnauthorized()
    {
        await Seed();
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => service.LoginAsync(
            new LoginRequestDto { Username = "doctor01", Password = "WrongPassword" }));
        Assert.Equal("Incorrect username or password.", ex.Message);
        Assert.Empty(tokens.Invocations);
    }

    // UTC-AUTH-003
    [Fact]
    public async Task LoginAsync_UnknownUser_ThrowsUnauthorized()
    {
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => service.LoginAsync(
            new LoginRequestDto { Username = "unknown", Password = "Test@123" }));
        Assert.Equal("Incorrect username or password.", ex.Message);
        Assert.Empty(tokens.Invocations);
    }

    // UTC-AUTH-004
    [Fact]
    public async Task LoginAsync_DisabledUser_ThrowsForbidden()
    {
        await Seed(false);
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => service.LoginAsync(
            new LoginRequestDto { Username = "doctor01", Password = "Test@123" }));
        Assert.Equal("The account has been disabled. Please contact an administrator.", ex.Message);
        Assert.Empty(tokens.Invocations);
    }

    // UTC-AUTH-005
    [Fact]
    public async Task RegisterAsync_ValidRequest_CreatesUser()
    {
        await Seed();
        var result = await service.RegisterAsync(Registration());
        var user = await db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .SingleAsync(u => u.Username == "newdoctor");
        Assert.Equal("new_hash", user.PasswordHash);
        Assert.True(user.IsActive);
        Assert.Contains(user.UserRoles, ur => ur.Role.RoleCode == "DOCTOR");
        Assert.Equal("valid_token", result.AccessToken);
        Assert.Contains("DOCTOR", result.User.Roles);
    }

    // UTC-AUTH-006
    [Fact]
    public async Task RegisterAsync_DuplicateUsername_ThrowsConflict()
    {
        await Seed();
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.RegisterAsync(Registration(username: "doctor01")));
        Assert.Equal("Username 'doctor01' is already in use.", ex.Message);
        Assert.Equal(1, await db.Users.CountAsync());
    }

    // UTC-AUTH-007
    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ThrowsConflict()
    {
        await Seed();
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.RegisterAsync(Registration(email: "doctor@test.com")));
        Assert.Equal("Email 'doctor@test.com' is already registered.", ex.Message);
        Assert.Equal(1, await db.Users.CountAsync());
    }

    // UTC-AUTH-008
    [Fact]
    public async Task UpdateProfileAsync_ValidRequest_UpdatesUser()
    {
        await Seed();
        var result = await service.UpdateProfileAsync(1, new UpdateProfileRequestDto
        { FullName = "Updated Doctor", Email = "UPDATED@test.com", Phone = "0912345678" });
        Assert.Equal("Updated Doctor", result.FullName);
        Assert.Equal("updated@test.com", result.Email);
        Assert.Equal("0912345678", result.Phone);
        db.ChangeTracker.Clear();
        var user = await db.Users.SingleAsync(u => u.UserId == 1);
        Assert.Equal(result.FullName, user.FullName);
        Assert.Equal(result.Email, user.Email);
        Assert.Equal(result.Phone, user.Phone);
    }

    // UTC-AUTH-009
    [Fact]
    public async Task UpdateProfileAsync_UnknownUser_ThrowsNotFound()
    {
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateProfileAsync(
            99, new UpdateProfileRequestDto { FullName = "Updated User" }));
        Assert.Equal("User 99 was not found.", ex.Message);
    }

    // UTC-AUTH-010
    [Fact]
    public async Task UpdateProfileAsync_WrongCurrentPassword_ThrowsBadRequest()
    {
        await Seed();
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => service.UpdateProfileAsync(
            1, new UpdateProfileRequestDto { CurrentPassword = "WrongPassword", NewPassword = "New@123" }));
        Assert.Equal("Mật khẩu hiện tại không chính xác.", ex.Message);
        hasher.Verify(x => x.HashPassword(It.IsAny<string>()), Times.Never);
        db.ChangeTracker.Clear();
        Assert.Equal("old_hash", (await db.Users.SingleAsync(u => u.UserId == 1)).PasswordHash);
    }

    public void Dispose() => db.Dispose();
}
