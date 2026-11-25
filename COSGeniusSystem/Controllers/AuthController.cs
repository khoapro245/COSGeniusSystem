using COSGeniusSystem.Services;
using System.Security.Claims;
using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace COSGeniusSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : BaseApiController
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Register a new user account
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponseModel<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseModel<ValidationErrorData>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponseModel<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponseModel<AuthResponseDto>>> Register([FromBody] RegisterRequestDto request, CancellationToken ct = default)
    {
        var result = await _authService.RegisterAsync(request, ct);
        return SuccessResponse(SystemMessages.UserRegistered, result);
    }

    /// <summary>
    /// Authenticate user and receive JWT access token
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponseModel<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseModel<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseModel<object>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponseModel<AuthResponseDto>>> Login([FromBody] LoginRequestDto request, CancellationToken ct = default)
    {
        var result = await _authService.LoginAsync(request, ct);
        return SuccessResponse(SystemMessages.LoginSuccessful, result);
    }

    /// <summary>
    /// Invalidate current JWT token and log out
    /// </summary>
    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(typeof(ApiResponseModel<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseModel<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponseModel<object?>>> Logout()
    {
        var authHeader = Request.Headers.Authorization.ToString();
        await _authService.LogoutAsync(authHeader);
        return SuccessResponse(SystemMessages.LogoutSuccessful);
    }

    /// <summary>
    /// Get current authenticated user's profile information
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponseModel<UserInfoResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseModel<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseModel<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponseModel<UserInfoResponseDto>>> GetCurrentUser(CancellationToken ct = default)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedException("The user identifier in the session is invalid.");
        }

        var profile = await _authService.GetCurrentUserProfileAsync(userId, ct);
        return SuccessResponse(SystemMessages.UserProfileRetrieved, profile);
    }

    /// <summary>
    /// Update current authenticated user's profile and optionally change password
    /// </summary>
    [Authorize]
    [HttpPut("profile")]
    [ProducesResponseType(typeof(ApiResponseModel<UserInfoResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseModel<ValidationErrorData>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponseModel<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseModel<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponseModel<UserInfoResponseDto>>> UpdateProfile(
        [FromBody] UpdateProfileRequestDto request, CancellationToken ct = default)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedException("The user identifier in the session is invalid.");
        }

        var profile = await _authService.UpdateProfileAsync(userId, request, ct);
        return SuccessResponse(SystemMessages.UserUpdated, profile);
    }
}
