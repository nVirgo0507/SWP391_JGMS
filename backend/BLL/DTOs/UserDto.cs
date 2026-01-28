using SWP391_JGMS.DAL.Models;

namespace SWP391_JGMS.BLL.DTOs;

public class UserDto
{
    public int UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? StudentCode { get; set; }
    public string? GithubUsername { get; set; }
    public string? JiraAccountId { get; set; }
    public string? Phone { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateUserDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    
    // Student-specific fields
    public string? StudentCode { get; set; }
    public string? GithubUsername { get; set; }
    public string? JiraAccountId { get; set; }
    
    // Lecturer-specific fields
    public string? Phone { get; set; }
}

public class UpdateUserDto
{
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public string? StudentCode { get; set; }
    public string? GithubUsername { get; set; }
    public string? JiraAccountId { get; set; }
    public string? Phone { get; set; }
    public UserStatus? Status { get; set; }
}

public class ChangePasswordDto
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
