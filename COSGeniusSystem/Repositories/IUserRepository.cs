using COSGeniusSystem.Models;

namespace COSGeniusSystem.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(int userId);
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> GetByEmailAsync(string email);
    Task<bool> ExistsByUsernameAsync(string username);
    Task<bool> ExistsByEmailAsync(string email);
    Task<User> AddAsync(User user);
    Task<Role?> GetRoleByCodeAsync(string roleCode);
    Task<Role> CreateRoleAsync(Role role);
    Task AssignRoleAsync(int userId, int roleId);
    Task SaveChangesAsync();
}
