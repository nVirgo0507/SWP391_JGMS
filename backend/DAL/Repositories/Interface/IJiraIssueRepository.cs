using DAL.Models;

namespace DAL.Repositories.Interface
{
    /// <summary>
    /// Repository for managing synced Jira Issues
    /// </summary>
    public interface IJiraIssueRepository
    {
        /// <summary>
        /// Get all Jira issues for a project
        /// </summary>
        System.Threading.Tasks.Task<List<JiraIssue>> GetByProjectIdAsync(int projectId);

        /// <summary>
        /// Get Jira issue by issue key (e.g., "SWP391-123")
        /// </summary>
        System.Threading.Tasks.Task<JiraIssue?> GetByIssueKeyAsync(string issueKey);

        /// <summary>
        /// Get Jira issue by Jira ID
        /// </summary>
        System.Threading.Tasks.Task<JiraIssue?> GetByJiraIdAsync(string jiraId);

        /// <summary>
        /// Get Jira issue by internal ID
        /// </summary>
        System.Threading.Tasks.Task<JiraIssue?> GetByIdAsync(int jiraIssueId);

        /// <summary>
        /// Add single Jira issue
        /// </summary>
        System.Threading.Tasks.Task AddAsync(JiraIssue issue);

        /// <summary>
        /// Add multiple Jira issues (bulk insert)
        /// </summary>
        System.Threading.Tasks.Task AddRangeAsync(List<JiraIssue> issues);

        /// <summary>
        /// Update existing Jira issue
        /// </summary>
        System.Threading.Tasks.Task UpdateAsync(JiraIssue issue);

        /// <summary>
        /// Get last sync time for a project
        /// </summary>
        System.Threading.Tasks.Task<DateTime?> GetLastSyncTimeAsync(int projectId);

        /// <summary>
        /// Get unassigned issues (no assignee_jira_id)
        /// </summary>
        System.Threading.Tasks.Task<List<JiraIssue>> GetUnassignedIssuesAsync(int projectId);

        /// <summary>
        /// Get issues by status
        /// </summary>
        System.Threading.Tasks.Task<List<JiraIssue>> GetByStatusAsync(int projectId, string status);
    }
}


