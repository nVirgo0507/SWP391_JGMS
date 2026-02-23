using DAL.Models;

namespace DAL.Repositories.Interface
{
    /// <summary>
    /// Repository for managing Jira Integration configurations
    /// </summary>
    public interface IJiraIntegrationRepository
    {
        /// <summary>
        /// Get Jira integration configuration by project ID
        /// </summary>
        System.Threading.Tasks.Task<JiraIntegration?> GetByProjectIdAsync(int projectId);

        /// <summary>
        /// Get Jira integration by ID
        /// </summary>
        System.Threading.Tasks.Task<JiraIntegration?> GetByIdAsync(int integrationId);

        /// <summary>
        /// Add new Jira integration configuration
        /// </summary>
        System.Threading.Tasks.Task AddAsync(JiraIntegration integration);

        /// <summary>
        /// Update existing Jira integration configuration
        /// </summary>
        System.Threading.Tasks.Task UpdateAsync(JiraIntegration integration);

        /// <summary>
        /// Delete Jira integration configuration
        /// </summary>
        System.Threading.Tasks.Task DeleteAsync(int integrationId);

        /// <summary>
        /// Check if Jira integration exists for a project
        /// </summary>
        System.Threading.Tasks.Task<bool> ExistsForProjectAsync(int projectId);

        /// <summary>
        /// Get all Jira integrations (admin only)
        /// </summary>
        System.Threading.Tasks.Task<List<JiraIntegration>> GetAllAsync();
    }
}


