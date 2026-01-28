using SWP391_JGMS.DAL.Models;

namespace SWP391_JGMS.DAL.Repositories;

public interface IUserRepository
{
    Task<IEnumerable<User>> GetAllAsync();
    Task<User?> GetByIdAsync(int id);
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByStudentCodeAsync(string studentCode);
    Task<User> CreateAsync(User user);
    Task<User> UpdateAsync(User user);
    Task<bool> DeleteAsync(int id);
    Task<bool> EmailExistsAsync(string email);
    Task<bool> StudentCodeExistsAsync(string studentCode);
    Task<IEnumerable<User>> GetByRoleAsync(UserRole role);
}
