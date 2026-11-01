using System.Security.Claims;
using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs.Users;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Services.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace COSGeniusSystem.Controllers;

[ApiController]
[Route("api/v1/users")]
public class UsersController : BaseApiController
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// API-USER-01: Lấy danh sách tài khoản người dùng kèm vai trò (chỉ dành cho ADMIN).
    /// GET /api/v1/users
    /// </summary>
    [HttpGet]
    [Authorize(Roles = RoleConstants.Admin)]
    public async Task<ActionResult<ApiResponseModel<List<UserResponseDto>>>> GetUsers(
        [FromQuery] UserFilterQuery query, CancellationToken ct = default)
    {
        var result = await _userService.GetUsersAsync(query, ct);
        return SuccessResponse(SystemMessages.UsersRetrieved, result);
    }

    /// <summary>
    /// API-USER-02: Lấy thông tin chi tiết một tài khoản (chỉ dành cho ADMIN).
    /// GET /api/v1/users/{id}
    /// </summary>
    [HttpGet("{id:int}")]
    [Authorize(Roles = RoleConstants.Admin)]
    public async Task<ActionResult<ApiResponseModel<UserResponseDto>>> GetUserById(int id, CancellationToken ct = default)
    {
        var user = await _userService.GetUserByIdAsync(id, ct);
        if (user is null)
            throw new NotFoundException($"User with id {id} was not found.");

        return SuccessResponse(SystemMessages.UserRetrieved, user);
    }

    /// <summary>
    /// API-USER-03: Đăng ký / Thêm mới tài khoản người dùng (chỉ dành cho ADMIN).
    /// POST /api/v1/users
    /// </summary>
    [HttpPost]
    [Authorize(Roles = RoleConstants.Admin)]
    public async Task<ActionResult<ApiResponseModel<UserResponseDto>>> CreateUser(
        [FromBody] CreateUserRequest request, CancellationToken ct = default)
    {
        var (dto, error) = await _userService.CreateUserAsync(request, ct);

        if (error is not null)
            throw new BadRequestException(error);

        return CreatedResponse(
            nameof(GetUserById),
            new { id = dto!.UserId },
            SystemMessages.UserCreated,
            dto);
    }

    /// <summary>
    /// API-USER-04: Cập nhật thông tin tài khoản, vai trò và mật khẩu (chỉ dành cho ADMIN).
    /// PUT /api/v1/users/{id}
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = RoleConstants.Admin)]
    public async Task<ActionResult<ApiResponseModel<UserResponseDto>>> UpdateUser(
        int id,
        [FromBody] UpdateUserRequest request,
        CancellationToken ct = default)
    {
        var (dto, error) = await _userService.UpdateUserAsync(id, request, ct);

        if (dto is null && error is null)
            throw new NotFoundException($"User with id {id} was not found.");

        if (error is not null)
            throw new BadRequestException(error);

        return SuccessResponse(SystemMessages.UserUpdated, dto!);
    }

    /// <summary>
    /// API-USER-05: Vô hiệu hóa hoặc kích hoạt lại tài khoản người dùng (chỉ dành cho ADMIN).
    /// PATCH /api/v1/users/{id}/toggle-status
    /// </summary>
    [HttpPatch("{id:int}/toggle-status")]
    [Authorize(Roles = RoleConstants.Admin)]
    public async Task<ActionResult<ApiResponseModel<object?>>> ToggleUserStatus(int id, CancellationToken ct = default)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        int.TryParse(userIdClaim, out var currentUserId);

        var (success, isActive, error) = await _userService.ToggleUserStatusAsync(id, currentUserId, ct);

        if (!success)
            throw new BadRequestException(error ?? "Không thể cập nhật trạng thái tài khoản.");

        var msg = isActive
            ? new SystemMessage(200, "Đã kích hoạt lại tài khoản thành công.", true)
            : new SystemMessage(200, "Đã vô hiệu hóa tài khoản thành công.", true);

        return SuccessResponse<object?>(msg, new { id, isActive });
    }

    /// <summary>
    /// API-USER-06: Danh sách các vai trò (Roles) trong hệ thống.
    /// GET /api/v1/users/roles
    /// </summary>
    [HttpGet("roles")]
    [Authorize]
    public async Task<ActionResult<ApiResponseModel<List<RoleDto>>>> GetRoles(CancellationToken ct = default)
    {
        var roles = await _userService.GetRolesAsync(ct);
        return SuccessResponse(SystemMessages.RolesRetrieved, roles);
    }
}
