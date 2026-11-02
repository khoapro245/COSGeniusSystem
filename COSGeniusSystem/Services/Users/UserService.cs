using COSGeniusSystem.Services;
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Services;

public class UserService : IUserService
{
    private readonly ApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;

    public UserService(ApplicationDbContext context, IPasswordHasher passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    public async Task<List<UserResponseDto>> GetUsersAsync(UserFilterQuery query, CancellationToken ct = default)
    {
        var q = _context.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .AsQueryable();

        if (query.IsActive.HasValue)
            q = q.Where(u => u.IsActive == query.IsActive.Value);

        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            var roleFilter = query.Role.Trim().ToUpper();
            q = q.Where(u => u.UserRoles.Any(ur => ur.Role.RoleCode.ToUpper() == roleFilter));
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim().ToLower();
            q = q.Where(u =>
                u.Username.ToLower().Contains(kw) ||
                u.FullName.ToLower().Contains(kw) ||
                (u.Email != null && u.Email.ToLower().Contains(kw)) ||
                (u.Phone != null && u.Phone.ToLower().Contains(kw)));
        }

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 50 : Math.Min(query.PageSize, 100);

        return await q
            .OrderBy(u => u.UserId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserResponseDto
            {
                UserId = u.UserId,
                Username = u.Username,
                FullName = u.FullName,
                Email = u.Email,
                Phone = u.Phone,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
                Roles = u.UserRoles.Select(ur => ur.Role.RoleCode).ToList()
            })
            .ToListAsync(ct);
    }

    public async Task<UserResponseDto?> GetUserByIdAsync(int id, CancellationToken ct = default)
    {
        var u = await _context.Users
            .AsNoTracking()
            .Include(x => x.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(x => x.UserId == id, ct);

        return u is null ? null : ToDto(u);
    }

    public async Task<(UserResponseDto? Dto, string? Error)> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var username = request.Username.Trim();
        if (await _context.Users.AnyAsync(u => u.Username.ToLower() == username.ToLower(), ct))
            return (null, $"Tên đăng nhập '{username}' đã được sử dụng.");

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var email = request.Email.Trim().ToLower();
            if (await _context.Users.AnyAsync(u => u.Email != null && u.Email.ToLower() == email, ct))
                return (null, $"Email '{request.Email}' đã được đăng ký.");
        }

        var roleCode = request.RoleCode.Trim().ToUpper();
        var role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleCode.ToUpper() == roleCode, ct);
        if (role == null)
        {
            role = new Role
            {
                RoleCode = roleCode,
                RoleName = roleCode,
                Description = $"Role for {roleCode}"
            };
            _context.Roles.Add(role);
            await _context.SaveChangesAsync(ct);
        }

        var user = new User
        {
            Username = username,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            FullName = request.FullName.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLower(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        user.UserRoles.Add(new UserRole
        {
            RoleId = role.RoleId,
            AssignedAt = DateTime.UtcNow
        });

        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        var dto = new UserResponseDto
        {
            UserId = user.UserId,
            Username = user.Username,
            FullName = user.FullName,
            Email = user.Email,
            Phone = user.Phone,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            Roles = new List<string> { role.RoleCode }
        };

        return (dto, null);
    }

    public async Task<(UserResponseDto? Dto, string? Error)> UpdateUserAsync(int id, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.UserId == id, ct);

        if (user is null)
            return (null, null);

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var email = request.Email.Trim().ToLower();
            var emailTaken = await _context.Users
                .AnyAsync(u => u.UserId != id && u.Email != null && u.Email.ToLower() == email, ct);

            if (emailTaken)
                return (null, $"Email '{request.Email}' đã thuộc về một tài khoản khác.");
        }

        user.FullName = request.FullName.Trim();
        user.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLower();
        user.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        user.IsActive = request.IsActive;

        if (!string.IsNullOrWhiteSpace(request.NewPassword))
        {
            user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword.Trim());
        }

        if (!string.IsNullOrWhiteSpace(request.RoleCode))
        {
            var targetRoleCode = request.RoleCode.Trim().ToUpper();
            var targetRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleCode.ToUpper() == targetRoleCode, ct);
            if (targetRole != null)
            {
                user.UserRoles.Clear();
                user.UserRoles.Add(new UserRole
                {
                    UserId = user.UserId,
                    RoleId = targetRole.RoleId,
                    AssignedAt = DateTime.UtcNow
                });
            }
        }

        await _context.SaveChangesAsync(ct);

        return (ToDto(user), null);
    }

    public async Task<(bool Success, bool IsActive, string? Error)> ToggleUserStatusAsync(int id, int currentUserId, CancellationToken ct = default)
    {
        if (id == currentUserId)
            return (false, true, "Bạn không thể tự vô hiệu hóa tài khoản quản trị đang đăng nhập của chính mình.");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == id, ct);
        if (user is null)
            return (false, false, "Không tìm thấy người dùng.");

        user.IsActive = !user.IsActive;
        await _context.SaveChangesAsync(ct);

        return (true, user.IsActive, null);
    }

    public async Task<List<RoleDto>> GetRolesAsync(CancellationToken ct = default)
    {
        return await _context.Roles
            .AsNoTracking()
            .OrderBy(r => r.RoleId)
            .Select(r => new RoleDto
            {
                RoleId = r.RoleId,
                RoleCode = r.RoleCode,
                RoleName = r.RoleName,
                Description = r.Description
            })
            .ToListAsync(ct);
    }

    private static UserResponseDto ToDto(User u)
    {
        return new UserResponseDto
        {
            UserId = u.UserId,
            Username = u.Username,
            FullName = u.FullName,
            Email = u.Email,
            Phone = u.Phone,
            IsActive = u.IsActive,
            CreatedAt = u.CreatedAt,
            Roles = u.UserRoles.Select(ur => ur.Role.RoleCode).ToList()
        };
    }
}

