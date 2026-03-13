using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs.Admin
{
    /// <summary>
    /// Request DTO to configure GitHub integration for a user
    /// </summary>
    public class GitHubConfigureRequest
    {
        [Required]
        public string GithubUsername { get; set; } = null!;
    }

    /// <summary>
    /// Request DTO to configure Jira integration for a user
    /// </summary>
    public class JiraConfigureRequest
    {
        [Required]
        public string JiraAccountId { get; set; } = null!;
    }

    /// <summary>
    /// DTO for user integration status information
    /// </summary>
    public class IntegrationStatusDTO
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = null!;
        public bool HasGithubIntegration { get; set; }
        public string? GithubUsername { get; set; }
        public bool HasJiraIntegration { get; set; }
        public string? JiraAccountId { get; set; }
        public DateTime ConfiguredAt { get; set; }
    }

    /// <summary>
    /// DTO for integration connectivity test result
    /// </summary>
    public class IntegrationTestResultDTO
    {
        public string IntegrationType { get; set; } = null!;
        public bool IsConnected { get; set; }
        public string Message { get; set; } = null!;
        public DateTime TestedAt { get; set; }
    }
}

