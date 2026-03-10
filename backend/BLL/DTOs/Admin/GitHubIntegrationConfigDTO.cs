namespace BLL.DTOs.Admin
{
    public class GitHubIntegrationConfigDTO
    {
        public string ApiToken { get; set; } = null!;
        public string RepoOwner { get; set; } = null!;
        public string RepoName { get; set; } = null!;
        public string RepoUrl { get; set; } = null!;
    }
}
