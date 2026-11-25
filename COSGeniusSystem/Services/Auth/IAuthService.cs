using COSGeniusSystem.DTOs;

namespace COSGeniusSystem.Services;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request, CancellationToken ct = default);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken ct = default);
    Task LogoutAsync(string? rawAuthorizationHeader);
    Task<UserInfoResponseDto> GetCurrentUserProfileAsync(int userId, CancellationToken ct = default);
    Task<UserInfoResponseDto> UpdateProfileAsync(int userId, UpdateProfileRequestDto request, CancellationToken ct = default);
}
