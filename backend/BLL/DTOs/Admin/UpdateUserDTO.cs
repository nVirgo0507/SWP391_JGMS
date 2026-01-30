using DAL.Models;
using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs.Admin
{
    public class UpdateUserDTO
    {
        [EmailAddress]
        public string? Email { get; set; }

        public string? FullName { get; set; }

        public UserRole? Role { get; set; }

        // Student-specific fields
        public string? StudentCode { get; set; }
        public string? GithubUsername { get; set; }
        public string? JiraAccountId { get; set; }

        // Lecturer-specific fields
        public string? Phone { get; set; }

        public UserStatus? Status { get; set; }
    }
}

