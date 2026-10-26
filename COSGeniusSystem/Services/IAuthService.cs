using COSGeniusSystem.DTOs.Auth;

namespace COSGeniusSystem.Services;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request);
    Task LogoutAsync(string? rawAuthorizationHeader);
    Task<UserInfoResponseDto> GetCurrentUserProfileAsync(int userId);
}
