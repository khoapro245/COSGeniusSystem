using COSGeniusSystem.DTOs.Users;

namespace COSGeniusSystem.Services.Users;

public interface IUserService
{
    Task<List<UserResponseDto>> GetUsersAsync(UserFilterQuery query, CancellationToken ct = default);
    Task<UserResponseDto?> GetUserByIdAsync(int id, CancellationToken ct = default);
    Task<(UserResponseDto? Dto, string? Error)> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<(UserResponseDto? Dto, string? Error)> UpdateUserAsync(int id, UpdateUserRequest request, CancellationToken ct = default);
    Task<(bool Success, bool IsActive, string? Error)> ToggleUserStatusAsync(int id, int currentUserId, CancellationToken ct = default);
    Task<List<RoleDto>> GetRolesAsync(CancellationToken ct = default);
}

