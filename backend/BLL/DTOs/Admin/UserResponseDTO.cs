using DAL.Models;

namespace BLL.DTOs.Admin
{
    public class UserResponseDTO
    {
        public int UserId { get; set; }
        public string Email { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public UserRole Role { get; set; }
        public string? StudentCode { get; set; }
        public string? GithubUsername { get; set; }
        public string? JiraAccountId { get; set; }
        public string? Phone { get; set; }
        public UserStatus? Status { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
