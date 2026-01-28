using SWP391_JGMS.BLL.DTOs;
using SWP391_JGMS.DAL.Models;
using SWP391_JGMS.DAL.Repositories;

namespace SWP391_JGMS.BLL.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;

    public UserService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<IEnumerable<UserDto>> GetAllUsersAsync()
    {
        var users = await _userRepository.GetAllAsync();
        return users.Select(MapToDto);
    }

    public async Task<UserDto?> GetUserByIdAsync(int id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        return user != null ? MapToDto(user) : null;
    }

    public async Task<UserDto?> GetUserByEmailAsync(string email)
    {
        var user = await _userRepository.GetByEmailAsync(email);
        return user != null ? MapToDto(user) : null;
    }

    public async Task<UserDto> CreateUserAsync(CreateUserDto createUserDto)
    {
        // Validate email uniqueness
        if (await _userRepository.EmailExistsAsync(createUserDto.Email))
        {
            throw new InvalidOperationException("Email already exists");
        }

        // Validate student code uniqueness for students
        if (createUserDto.Role == UserRole.Student && !string.IsNullOrEmpty(createUserDto.StudentCode))
        {
            if (await _userRepository.StudentCodeExistsAsync(createUserDto.StudentCode))
            {
                throw new InvalidOperationException("Student code already exists");
            }
        }

        var user = new User
        {
            Email = createUserDto.Email,
            PasswordHash = HashPassword(createUserDto.Password), // Simple hash for now
            FullName = createUserDto.FullName,
            Role = createUserDto.Role,
            StudentCode = createUserDto.StudentCode,
            GithubUsername = createUserDto.GithubUsername,
            JiraAccountId = createUserDto.JiraAccountId,
            Phone = createUserDto.Phone,
            Status = UserStatus.Active
        };

        var createdUser = await _userRepository.CreateAsync(user);
        return MapToDto(createdUser);
    }

    public async Task<UserDto> UpdateUserAsync(int id, UpdateUserDto updateUserDto)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null)
        {
            throw new KeyNotFoundException($"User with ID {id} not found");
        }

        // Update only provided fields
        if (!string.IsNullOrEmpty(updateUserDto.Email))
        {
            if (updateUserDto.Email != user.Email && await _userRepository.EmailExistsAsync(updateUserDto.Email))
            {
                throw new InvalidOperationException("Email already exists");
            }
            user.Email = updateUserDto.Email;
        }

        if (!string.IsNullOrEmpty(updateUserDto.FullName))
            user.FullName = updateUserDto.FullName;

        if (!string.IsNullOrEmpty(updateUserDto.StudentCode))
        {
            if (updateUserDto.StudentCode != user.StudentCode && 
                await _userRepository.StudentCodeExistsAsync(updateUserDto.StudentCode))
            {
                throw new InvalidOperationException("Student code already exists");
            }
            user.StudentCode = updateUserDto.StudentCode;
        }

        if (!string.IsNullOrEmpty(updateUserDto.GithubUsername))
            user.GithubUsername = updateUserDto.GithubUsername;

        if (!string.IsNullOrEmpty(updateUserDto.JiraAccountId))
            user.JiraAccountId = updateUserDto.JiraAccountId;

        if (!string.IsNullOrEmpty(updateUserDto.Phone))
            user.Phone = updateUserDto.Phone;

        if (updateUserDto.Status.HasValue)
            user.Status = updateUserDto.Status.Value;

        var updatedUser = await _userRepository.UpdateAsync(user);
        return MapToDto(updatedUser);
    }

    public async Task<bool> DeleteUserAsync(int id)
    {
        return await _userRepository.DeleteAsync(id);
    }

    public async Task<bool> ChangePasswordAsync(int id, ChangePasswordDto changePasswordDto)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null)
        {
            throw new KeyNotFoundException($"User with ID {id} not found");
        }

        // Verify current password
        if (user.PasswordHash != HashPassword(changePasswordDto.CurrentPassword))
        {
            throw new UnauthorizedAccessException("Current password is incorrect");
        }

        user.PasswordHash = HashPassword(changePasswordDto.NewPassword);
        await _userRepository.UpdateAsync(user);
        return true;
    }

    public async Task<IEnumerable<UserDto>> GetUsersByRoleAsync(UserRole role)
    {
        var users = await _userRepository.GetByRoleAsync(role);
        return users.Select(MapToDto);
    }

    // Helper methods
    private static UserDto MapToDto(User user)
    {
        return new UserDto
        {
            UserId = user.UserId,
            Email = user.Email,
            FullName = user.FullName,
            Role = user.Role.ToString().ToLower(),
            StudentCode = user.StudentCode,
            GithubUsername = user.GithubUsername,
            JiraAccountId = user.JiraAccountId,
            Phone = user.Phone,
            Status = user.Status.ToString().ToLower(),
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }

    private static string HashPassword(string password)
    {
        // Simple hash for now - replace with BCrypt or similar in production
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(password));
    }
}
