using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs.Student
{
    /// <summary>
    /// DTO for students to update their basic profile information
    /// </summary>
    public class UpdateProfileDTO
    {
        [Phone]
        public string? Phone { get; set; }
        
        public string? GithubUsername { get; set; }
        
        public string? JiraAccountId { get; set; }
    }
}
