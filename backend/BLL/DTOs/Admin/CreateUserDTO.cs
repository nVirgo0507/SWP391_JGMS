using DAL.Models;
using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs.Admin
{
    public class CreateUserDTO
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;

        [Required]
        [MinLength(8)]
        public string Password { get; set; } = null!;

        [Required]
        public string FullName { get; set; } = null!;

        [Required]
        public UserRole Role { get; set; }

        // Student-specific fields
        public string? StudentCode { get; set; }
        public string? GithubUsername { get; set; }
        public string? JiraAccountId { get; set; }

        // Lecturer-specific fields
        public string? Phone { get; set; }

        public UserStatus Status { get; set; } = UserStatus.active;
    }
}
