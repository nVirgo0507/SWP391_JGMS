namespace BLL.DTOs.Jira
{
    /// <summary>
    /// DTO for Jira issue information
    /// </summary>
    public class JiraIssueDTO
    {
        public int JiraIssueId { get; set; }
        public string IssueKey { get; set; } = null!;
        public string JiraId { get; set; } = null!;
        public string IssueType { get; set; } = null!;
        public string Summary { get; set; } = null!;
        public string? Description { get; set; }
        public string? Priority { get; set; }
        public string Status { get; set; } = null!;
        public string? AssigneeJiraId { get; set; }
        public string? AssigneeName { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public DateTime? LastSynced { get; set; }
    }

    /// <summary>
    /// DTO for Jira sync operation result
    /// </summary>
    public class JiraSyncResultDTO
    {
        public int TotalIssues { get; set; }
        public int NewIssues { get; set; }
        public int UpdatedIssues { get; set; }
        public int FailedIssues { get; set; }
        public DateTime SyncTime { get; set; }
        public string Status { get; set; } = null!;
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    /// <summary>
    /// DTO for Jira project information
    /// </summary>
    public class JiraProjectDTO
    {
        public string Id { get; set; } = null!;
        public string Key { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public string ProjectTypeKey { get; set; } = null!;
    }

    /// <summary>
    /// DTO for creating a Jira issue
    /// </summary>
    public class CreateJiraIssueDTO
    {
        public string ProjectKey { get; set; } = null!;
        public string Summary { get; set; } = null!;
        public string? Description { get; set; }
        public string IssueType { get; set; } = "Task";
        public string? Priority { get; set; }
        public string? AssigneeAccountId { get; set; }
    }

    /// <summary>
    /// DTO for updating a Jira issue
    /// </summary>
    public class UpdateJiraIssueDTO
    {
        public string? Summary { get; set; }
        public string? Description { get; set; }
        public string? AssigneeAccountId { get; set; }
        public string? Priority { get; set; }
    }

    /// <summary>
    /// DTO for Jira issue transition (status change)
    /// </summary>
    public class JiraTransitionDTO
    {
        public string Id { get; set; } = null!;
        public string Name { get; set; } = null!;
        public JiraStatusDTO To { get; set; } = null!;
    }

    /// <summary>
    /// DTO for Jira status
    /// </summary>
    public class JiraStatusDTO
    {
        public string Id { get; set; } = null!;
        public string Name { get; set; } = null!;
    }
}

