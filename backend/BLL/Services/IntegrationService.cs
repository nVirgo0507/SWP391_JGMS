using BLL.DTOs.Admin;
using BLL.Services.Interface;
using DAL.Models;
using DAL.Repositories.Interface;
using System.Threading.Tasks;

namespace BLL.Services
{
    /// <summary>
    /// Integration Service
    /// BR-058: Only Admin Configures Integrations - Only admin users can configure Jira and GitHub integrations
    /// Validation: Check user role = 'admin'
    /// Error Message: "Only administrators can configure integrations"
    /// </summary>
    public class IntegrationService : IIntegrationService
    {
        private readonly IUserRepository _userRepository;

        public IntegrationService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        /// <summary>
        /// BR-058: Validates that user has admin role
        /// Throws exception if user is not admin
        /// </summary>
        private async System.Threading.Tasks.Task ValidateAdminAccessAsync(int userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null || user.Role != UserRole.admin)
            {
                throw new Exception("Only administrators can configure integrations");
            }
        }

        #region GitHub Integration

        /// <summary>
        /// BR-058: Configure GitHub integration for a user
        /// Only admin users can configure integrations
        /// Validates user role is admin before allowing configuration
        /// </summary>
        public async Task<UserResponseDTO> ConfigureGithubAsync(int adminUserId, int targetUserId, string githubUsername)
        {
            // BR-058: Validate admin user has permission to configure integrations
            await ValidateAdminAccessAsync(adminUserId);

            // Verify target user exists
            var targetUser = await _userRepository.GetByIdAsync(targetUserId);
            if (targetUser == null)
            {
                throw new Exception("Target user not found");
            }

            // Update GitHub integration
            targetUser.GithubUsername = githubUsername;
            // TODO: Update user in repository
            // await _userRepository.UpdateAsync(targetUser);

            // Return updated user response
            return new UserResponseDTO
            {
                UserId = targetUser.UserId,
                Email = targetUser.Email,
                FullName = targetUser.FullName,
                Role = targetUser.Role,
                Status = targetUser.Status,
                GithubUsername = targetUser.GithubUsername,
                JiraAccountId = targetUser.JiraAccountId
            };
        }

        /// <summary>
        /// BR-058: Remove GitHub integration for a user
        /// Only admin users can remove integrations
        /// </summary>
        public async Task<UserResponseDTO> RemoveGithubAsync(int adminUserId, int targetUserId)
        {
            // BR-058: Validate admin user has permission to configure integrations
            await ValidateAdminAccessAsync(adminUserId);

            // Verify target user exists
            var targetUser = await _userRepository.GetByIdAsync(targetUserId);
            if (targetUser == null)
            {
                throw new Exception("Target user not found");
            }

            // Remove GitHub integration
            targetUser.GithubUsername = null;
            // TODO: Update user in repository
            // await _userRepository.UpdateAsync(targetUser);

            // Return updated user response
            return new UserResponseDTO
            {
                UserId = targetUser.UserId,
                Email = targetUser.Email,
                FullName = targetUser.FullName,
                Role = targetUser.Role,
                Status = targetUser.Status,
                GithubUsername = targetUser.GithubUsername,
                JiraAccountId = targetUser.JiraAccountId
            };
        }

        #endregion

        #region Jira Integration

        /// <summary>
        /// BR-058: Configure Jira integration for a user
        /// Only admin users can configure integrations
        /// Validates user role is admin before allowing configuration
        /// </summary>
        public async Task<UserResponseDTO> ConfigureJiraAsync(int adminUserId, int targetUserId, string jiraAccountId)
        {
            // BR-058: Validate admin user has permission to configure integrations
            await ValidateAdminAccessAsync(adminUserId);

            // Verify target user exists
            var targetUser = await _userRepository.GetByIdAsync(targetUserId);
            if (targetUser == null)
            {
                throw new Exception("Target user not found");
            }

            // Update Jira integration
            targetUser.JiraAccountId = jiraAccountId;
            // TODO: Update user in repository
            // await _userRepository.UpdateAsync(targetUser);

            // Return updated user response
            return new UserResponseDTO
            {
                UserId = targetUser.UserId,
                Email = targetUser.Email,
                FullName = targetUser.FullName,
                Role = targetUser.Role,
                Status = targetUser.Status,
                GithubUsername = targetUser.GithubUsername,
                JiraAccountId = targetUser.JiraAccountId
            };
        }

        /// <summary>
        /// BR-058: Remove Jira integration for a user
        /// Only admin users can remove integrations
        /// </summary>
        public async Task<UserResponseDTO> RemoveJiraAsync(int adminUserId, int targetUserId)
        {
            // BR-058: Validate admin user has permission to configure integrations
            await ValidateAdminAccessAsync(adminUserId);

            // Verify target user exists
            var targetUser = await _userRepository.GetByIdAsync(targetUserId);
            if (targetUser == null)
            {
                throw new Exception("Target user not found");
            }

            // Remove Jira integration
            targetUser.JiraAccountId = null;
            // TODO: Update user in repository
            // await _userRepository.UpdateAsync(targetUser);

            // Return updated user response
            return new UserResponseDTO
            {
                UserId = targetUser.UserId,
                Email = targetUser.Email,
                FullName = targetUser.FullName,
                Role = targetUser.Role,
                Status = targetUser.Status,
                GithubUsername = targetUser.GithubUsername,
                JiraAccountId = targetUser.JiraAccountId
            };
        }

        #endregion

        #region Integration Management

        /// <summary>
        /// BR-058: Get all configured integrations
        /// Only admin users can view all integrations
        /// </summary>
        public async Task<List<IntegrationStatusDTO>> GetAllIntegrationsAsync(int adminUserId)
        {
            // BR-058: Validate admin user has permission
            await ValidateAdminAccessAsync(adminUserId);

            // TODO: Get all users with configured integrations from repository
            // Filter users where GithubUsername is not null OR JiraAccountId is not null
            return new List<IntegrationStatusDTO>();
        }

        /// <summary>
        /// BR-058: Verify integration connectivity
        /// Only admin users can test integrations
        /// Tests connection to GitHub or Jira API
        /// </summary>
        public async Task<IntegrationTestResultDTO> TestIntegrationAsync(int adminUserId, string integrationType)
        {
            // BR-058: Validate admin user has permission
            await ValidateAdminAccessAsync(adminUserId);

            // Validate integration type
            if (!integrationType.Equals("github", StringComparison.OrdinalIgnoreCase) &&
                !integrationType.Equals("jira", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception("Invalid integration type. Supported types: GitHub, Jira");
            }

            // TODO: Test API connectivity based on integration type
            // For GitHub: Test connection to GitHub API with configured credentials
            // For Jira: Test connection to Jira API with configured credentials

            return new IntegrationTestResultDTO
            {
                IntegrationType = integrationType,
                IsConnected = false,
                Message = "Integration test not yet implemented",
                TestedAt = DateTime.UtcNow
            };
        }

        #endregion
    }
}
