using System;

namespace BLL.DTOs.Github
{
    /// <summary>
    /// Safe GitHub integration payload for frontend display (no raw token).
    /// </summary>
    public class GithubIntegrationResponseDto
    {
        public int IntegrationId { get; set; }
        public int ProjectId { get; set; }
        public string RepoUrl { get; set; } = string.Empty;
        public string RepoOwner { get; set; } = string.Empty;
        public string RepoName { get; set; } = string.Empty;
        public bool HasTokenConfigured { get; set; }
        public string SyncStatus { get; set; } = string.Empty;
        public DateTime? LastSync { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}


