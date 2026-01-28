using SWP391_JGMS.BLL.DTOs;
using SWP391_JGMS.DAL.Models;

namespace SWP391_JGMS.BLL.Services;

public interface IUserService
{
    Task<IEnumerable<UserDto>> GetAllUsersAsync();
    Task<UserDto?> GetUserByIdAsync(int id);
    Task<UserDto?> GetUserByEmailAsync(string email);
    Task<UserDto> CreateUserAsync(CreateUserDto createUserDto);
    Task<UserDto> UpdateUserAsync(int id, UpdateUserDto updateUserDto);
    Task<bool> DeleteUserAsync(int id);
    Task<bool> ChangePasswordAsync(int id, ChangePasswordDto changePasswordDto);
    Task<IEnumerable<UserDto>> GetUsersByRoleAsync(UserRole role);
}
