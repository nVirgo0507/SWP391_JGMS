using BLL.DTOs.Jira;

namespace BLL.Services.Interface
{
    /// <summary>
    /// Service for managing Jira integration configuration and synchronization
    /// </summary>
    public interface IJiraIntegrationService
    {
        // ============================================================================
        // Admin: Configuration Management
        // ============================================================================

        /// <summary>
        /// Configure Jira integration for a project (Admin only)
        /// </summary>
        Task<JiraIntegrationResponseDTO> ConfigureIntegrationAsync(int adminUserId, int projectId, ConfigureJiraIntegrationDTO dto);

        /// <summary>
        /// Get Jira integration configuration for a project
        /// </summary>
        Task<JiraIntegrationResponseDTO?> GetIntegrationAsync(int userId, int projectId);

        /// <summary>
        /// Update Jira integration configuration (Admin only)
        /// </summary>
        Task<JiraIntegrationResponseDTO> UpdateIntegrationAsync(int adminUserId, int projectId, ConfigureJiraIntegrationDTO dto);

        /// <summary>
        /// Delete Jira integration configuration (Admin only)
        /// </summary>
        Task DeleteIntegrationAsync(int adminUserId, int projectId);

        /// <summary>
        /// Test Jira connection (Admin only)
        /// </summary>
        Task<JiraConnectionTestDTO> TestConnectionAsync(int adminUserId, int projectId);

        /// <summary>
        /// Get all Jira integrations (Admin only)
        /// </summary>
        Task<List<JiraIntegrationResponseDTO>> GetAllIntegrationsAsync(int adminUserId);

        // ============================================================================
        // Synchronization
        // ============================================================================

        /// <summary>
        /// Sync issues from Jira to database (Admin or Leader)
        /// </summary>
        Task<JiraSyncResultDTO> SyncIssuesAsync(int userId, int projectId);

        /// <summary>
        /// Get sync history/status
        /// </summary>
        Task<JiraSyncResultDTO> GetSyncStatusAsync(int userId, int projectId);

        // ============================================================================
        // Issue Viewing (Role-based)
        // ============================================================================

        /// <summary>
        /// Get all synced Jira issues for a project (role-based filtering)
        /// - Admin: All issues
        /// - Lecturer: All issues in their assigned groups (read-only)
        /// - Leader: All issues in their project
        /// - Student: Only assigned issues
        /// </summary>
        Task<List<JiraIssueDTO>> GetProjectIssuesAsync(int userId, int projectId);

        /// <summary>
        /// Get single issue details
        /// </summary>
        Task<JiraIssueDTO> GetIssueDetailsAsync(int userId, string issueKey);
    }
}

