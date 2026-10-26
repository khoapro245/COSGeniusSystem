using COSGeniusSystem.Common;
using COSGeniusSystem.DTOs.Auth;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Model;
using COSGeniusSystem.Repositories;

namespace COSGeniusSystem.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly ITokenBlacklistService _tokenBlacklistService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        ITokenBlacklistService tokenBlacklistService,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _tokenBlacklistService = tokenBlacklistService;
        _logger = logger;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request)
    {
        // Check if username already exists
        if (await _userRepository.ExistsByUsernameAsync(request.Username))
        {
            throw new ConflictException($"Username '{request.Username}' is already taken.");
        }

        // Check if email already exists
        if (!string.IsNullOrWhiteSpace(request.Email) && await _userRepository.ExistsByEmailAsync(request.Email))
        {
            throw new ConflictException($"Email '{request.Email}' is already registered.");
        }

        // Determine role to assign
        var targetRoleCode = string.IsNullOrWhiteSpace(request.RoleCode)
            ? RoleConstants.User
            : request.RoleCode.Trim().ToUpper();

        var role = await _userRepository.GetRoleByCodeAsync(targetRoleCode);
        if (role == null)
        {
            // Seed the role if it doesn't exist
            role = await _userRepository.CreateRoleAsync(new Role
            {
                RoleCode = targetRoleCode,
                RoleName = targetRoleCode,
                Description = $"Role for {targetRoleCode}"
            });
            await _userRepository.SaveChangesAsync();
        }

        var newUser = new User
        {
            Username = request.Username.Trim(),
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            FullName = request.FullName.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLower(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        newUser.UserRoles.Add(new UserRole
        {
            RoleId = role.RoleId,
            AssignedAt = DateTime.UtcNow
        });

        await _userRepository.AddAsync(newUser);
        await _userRepository.SaveChangesAsync();

        _logger.LogInformation("New user registered successfully with ID: {UserId}, Role: {RoleCode}", newUser.UserId, role.RoleCode);

        var roles = new List<string> { role.RoleCode };
        var (token, expiresIn) = _tokenService.GenerateToken(newUser, roles);

        return new AuthResponseDto
        {
            AccessToken = token,
            TokenType = "Bearer",
            ExpiresIn = expiresIn,
            User = MapToUserInfoResponseDto(newUser, roles)
        };
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request)
    {
        var user = await _userRepository.GetByUsernameAsync(request.Username);
        if (user == null || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedException("Invalid username or password.");
        }

        if (!user.IsActive)
        {
            throw new ForbiddenException("Your account is currently disabled. Please contact the administrator.");
        }

        var roles = user.UserRoles.Select(ur => ur.Role.RoleCode).ToList();
        var (token, expiresIn) = _tokenService.GenerateToken(user, roles);

        _logger.LogInformation("User logged in successfully: {Username}", user.Username);

        return new AuthResponseDto
        {
            AccessToken = token,
            TokenType = "Bearer",
            ExpiresIn = expiresIn,
            User = MapToUserInfoResponseDto(user, roles)
        };
    }

    public Task LogoutAsync(string? rawAuthorizationHeader)
    {
        if (string.IsNullOrWhiteSpace(rawAuthorizationHeader))
        {
            return Task.CompletedTask;
        }

        var token = rawAuthorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? rawAuthorizationHeader["Bearer ".Length..].Trim()
            : rawAuthorizationHeader.Trim();

        if (!string.IsNullOrWhiteSpace(token))
        {
            var remaining = _tokenService.GetTokenRemainingLifetime(token);
            _tokenBlacklistService.BlacklistToken(token, remaining);
            _logger.LogInformation("Token blacklisted successfully for remaining duration: {Duration}", remaining);
        }

        return Task.CompletedTask;
    }

    public async Task<UserInfoResponseDto> GetCurrentUserProfileAsync(int userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            throw new NotFoundException($"User with ID {userId} was not found.");
        }

        var roles = user.UserRoles.Select(ur => ur.Role.RoleCode).ToList();
        return MapToUserInfoResponseDto(user, roles);
    }

    private static UserInfoResponseDto MapToUserInfoResponseDto(User user, List<string> roles)
    {
        return new UserInfoResponseDto
        {
            UserId = user.UserId,
            Username = user.Username,
            FullName = user.FullName,
            Email = user.Email,
            Phone = user.Phone,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            Roles = roles
        };
    }
}
