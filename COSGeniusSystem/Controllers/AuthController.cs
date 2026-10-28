using System.Security.Claims;
using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs.Auth;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Services;
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
    public async Task<ActionResult<ApiResponseModel<AuthResponseDto>>> Register([FromBody] RegisterRequestDto request)
    {
        var result = await _authService.RegisterAsync(request);
        return SuccessResponse(SystemMessages.UserRegistered, result);
    }

    /// <summary>
    /// Authenticate user and receive JWT access token
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponseModel<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseModel<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseModel<object>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponseModel<AuthResponseDto>>> Login([FromBody] LoginRequestDto request)
    {
        var result = await _authService.LoginAsync(request);
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
    public async Task<ActionResult<ApiResponseModel<UserInfoResponseDto>>> GetCurrentUser()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedException("Invalid token user identifier.");
        }

        var profile = await _authService.GetCurrentUserProfileAsync(userId);
        return SuccessResponse(SystemMessages.UserProfileRetrieved, profile);
    }
}
