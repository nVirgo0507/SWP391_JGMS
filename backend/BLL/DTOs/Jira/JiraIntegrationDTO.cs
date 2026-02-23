using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs.Jira
{
    /// <summary>
    /// DTO for configuring Jira integration (Admin only)
    /// </summary>
    public class ConfigureJiraIntegrationDTO
    {
        [Required(ErrorMessage = "Jira URL is required")]
        [Url(ErrorMessage = "Invalid Jira URL format")]
        public string JiraUrl { get; set; } = null!;

        [Required(ErrorMessage = "Jira email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string JiraEmail { get; set; } = null!;

        [Required(ErrorMessage = "API token is required")]
        [MinLength(10, ErrorMessage = "API token must be at least 10 characters")]
        public string ApiToken { get; set; } = null!;

        [Required(ErrorMessage = "Project key is required")]
        [RegularExpression(@"^[A-Z][A-Z0-9_]*$", ErrorMessage = "Project key must be uppercase letters, numbers, or underscores")]
        public string ProjectKey { get; set; } = null!;
    }

    /// <summary>
    /// DTO for Jira integration response
    /// </summary>
    public class JiraIntegrationResponseDTO
    {
        public int IntegrationId { get; set; }
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = null!;
        public string JiraUrl { get; set; } = null!;
        public string JiraEmail { get; set; } = null!;
        public string ProjectKey { get; set; } = null!;
        public DateTime? LastSync { get; set; }
        public string SyncStatus { get; set; } = null!;
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// DTO for Jira connection test result
    /// </summary>
    public class JiraConnectionTestDTO
    {
        public bool IsConnected { get; set; }
        public string Message { get; set; } = null!;
        public string? JiraProjectName { get; set; }
        public string? JiraProjectKey { get; set; }
        public DateTime TestedAt { get; set; }
    }
}

