namespace BLL.DTOs.Student
{
    /// <summary>
    /// Optional customization for commit line generation.
    /// </summary>
    public class CommitLineSuggestionRequestDTO
    {
        /// <summary>
        /// Internal task ID. Provide this or JiraIssueKey.
        /// </summary>
        public int? TaskId { get; set; }

        /// <summary>
        /// Jira issue key (e.g. SWP391-123). Provide this or TaskId.
        /// </summary>
        public string? JiraIssueKey { get; set; }

        /// <summary>
        /// Conventional commit type (feat, fix, docs, refactor, test, chore).
        /// If omitted, the service infers a sensible default from Jira/task metadata.
        /// </summary>
        public string? Type { get; set; }

        /// <summary>
        /// Optional scope segment for conventional commits, e.g. "api" or "jira-sync".
        /// </summary>
        public string? Scope { get; set; }

        /// <summary>
        /// Include Jira issue key (or task fallback) in the generated line prefix.
        /// </summary>
        public bool IncludeIssueKey { get; set; } = true;
    }

    /// <summary>
    /// Commit line suggestion payload returned to the frontend.
    /// </summary>
    public class CommitLineSuggestionResponseDTO
    {
        public int? TaskId { get; set; }
        public string? JiraIssueKey { get; set; }
        public string CommitLine { get; set; } = string.Empty;
        public string GitCommitCommand { get; set; } = string.Empty;
        public string GitHubCommand { get; set; } = string.Empty;
        public List<string> Alternatives { get; set; } = new();
    }
}


