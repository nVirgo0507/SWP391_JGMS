using BLL.DTOs.Admin;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    /// <summary>
    /// Integration Service Interface
    /// BR-058: Only Admin Configures Integrations - Only admin users can configure Jira and GitHub integrations
    /// Validation: Check user role = 'admin'
    /// Error Message: "Only administrators can configure integrations"
    /// </summary>
    public interface IIntegrationService
    {
        #region GitHub Integration

        /// <summary>
        /// BR-058: Configure GitHub integration for a user
        /// Only admin users can configure integrations
        /// Validates user role is admin before allowing configuration
        /// </summary>
        Task<UserResponseDTO> ConfigureGithubAsync(int adminUserId, int targetUserId, string githubUsername);

        /// <summary>
        /// BR-058: Remove GitHub integration for a user
        /// Only admin users can remove integrations
        /// </summary>
        Task<UserResponseDTO> RemoveGithubAsync(int adminUserId, int targetUserId);

        #endregion

        #region Jira Integration

        /// <summary>
        /// BR-058: Configure Jira integration for a user
        /// Only admin users can configure integrations
        /// Validates user role is admin before allowing configuration
        /// </summary>
        Task<UserResponseDTO> ConfigureJiraAsync(int adminUserId, int targetUserId, string jiraAccountId);

        /// <summary>
        /// BR-058: Remove Jira integration for a user
        /// Only admin users can remove integrations
        /// </summary>
        Task<UserResponseDTO> RemoveJiraAsync(int adminUserId, int targetUserId);

        #endregion

        #region Integration Management

        /// <summary>
        /// BR-058: Get all configured integrations
        /// Only admin users can view all integrations
        /// </summary>
        Task<List<IntegrationStatusDTO>> GetAllIntegrationsAsync(int adminUserId);

        /// <summary>
        /// BR-058: Verify integration connectivity
        /// Only admin users can test integrations
        /// Tests connection to GitHub or Jira API
        /// </summary>
        Task<IntegrationTestResultDTO> TestIntegrationAsync(int adminUserId, string integrationType);

        #endregion
    }
}
