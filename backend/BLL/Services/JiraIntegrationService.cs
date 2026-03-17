using BLL.DTOs.Jira;
using BLL.Services.Interface;
using DAL.Models;
using DAL.Repositories.Interface;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace BLL.Services
{
    /// <summary>
    /// Service for managing Jira integration and synchronization
    /// Implements role-based access control for Jira operations
    /// </summary>
    public class JiraIntegrationService : IJiraIntegrationService
    {
        private readonly IJiraIntegrationRepository _jiraIntegrationRepo;
        private readonly IJiraIssueRepository _jiraIssueRepo;
        private readonly IProjectRepository _projectRepo;
        private readonly IUserRepository _userRepo;
        private readonly IStudentGroupRepository _groupRepo;
        private readonly IGroupMemberRepository _groupMemberRepo;
        private readonly IJiraApiService _jiraApiService;
        private readonly byte[] _encryptionKey;

        public JiraIntegrationService(
            IJiraIntegrationRepository jiraIntegrationRepo,
            IJiraIssueRepository jiraIssueRepo,
            IProjectRepository projectRepo,
            IUserRepository userRepo,
            IStudentGroupRepository groupRepo,
            IGroupMemberRepository groupMemberRepo,
            IJiraApiService jiraApiService,
            IConfiguration configuration)
        {
            _jiraIntegrationRepo = jiraIntegrationRepo;
            _jiraIssueRepo = jiraIssueRepo;
            _projectRepo = projectRepo;
            _userRepo = userRepo;
            _groupRepo = groupRepo;
            _groupMemberRepo = groupMemberRepo;
            _jiraApiService = jiraApiService;

            // Derive a stable 256-bit AES key from the JWT secret using SHA-256.
            // This is stable across restarts (unlike IDataProtector ephemeral keys).
            var jwtKey = configuration["Jwt:Key"] ?? "JGMS_DEFAULT_ENCRYPTION_KEY_32CH";
            _encryptionKey = SHA256.HashData(Encoding.UTF8.GetBytes(jwtKey));
        }

        // ============================================================================
        // Helper Methods
        // ============================================================================

        private async Task<User> ValidateUserAsync(int userId)
        {
            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null)
            {
                throw new Exception("User not found");
            }
            return user;
        }

        private void ValidateAdminRole(User user)
        {
            if (user.Role != UserRole.admin)
            {
                throw new UnauthorizedAccessException("Only administrators can configure Jira integration");
            }
        }

        private async Task<bool> IsUserProjectLeaderAsync(int userId, int projectId)
        {
            var project = await _projectRepo.GetByIdAsync(projectId);
            if (project == null) return false;

            var group = await _groupRepo.GetByIdAsync(project.GroupId);
            return group?.LeaderId == userId;
        }

        private async Task<bool> IsUserProjectLecturerAsync(int userId, int projectId)
        {
            var project = await _projectRepo.GetByIdAsync(projectId);
            if (project == null) return false;

            var group = await _groupRepo.GetByIdAsync(project.GroupId);
            return group?.LecturerId == userId;
        }

        private async Task<bool> IsUserProjectMemberAsync(int userId, int projectId)
        {
            var project = await _projectRepo.GetByIdAsync(projectId);
            if (project == null) return false;

            var members = await _groupMemberRepo.GetByGroupIdAsync(project.GroupId);
            return members.Any(m => m.UserId == userId);
        }

        private string EncryptToken(string token)
        {
            var plaintext = Encoding.UTF8.GetBytes(token.Trim());
            var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];   // 12 bytes
            var tag   = new byte[AesGcm.TagByteSizes.MaxSize];     // 16 bytes
            var ciphertext = new byte[plaintext.Length];

            RandomNumberGenerator.Fill(nonce);

            using var aes = new AesGcm(_encryptionKey, AesGcm.TagByteSizes.MaxSize);
            aes.Encrypt(nonce, plaintext, ciphertext, tag);

            // Layout: base64(nonce || tag || ciphertext)
            var combined = new byte[nonce.Length + tag.Length + ciphertext.Length];
            nonce.CopyTo(combined, 0);
            tag.CopyTo(combined, nonce.Length);
            ciphertext.CopyTo(combined, nonce.Length + tag.Length);
            return Convert.ToBase64String(combined);
        }

        private string DecryptToken(string encryptedToken)
        {
            try
            {
                var data = Convert.FromBase64String(encryptedToken);
                var nonceSize = AesGcm.NonceByteSizes.MaxSize;  // 12
                var tagSize   = AesGcm.TagByteSizes.MaxSize;     // 16

                var nonce      = data[..nonceSize];
                var tag        = data[nonceSize..(nonceSize + tagSize)];
                var ciphertext = data[(nonceSize + tagSize)..];
                var plaintext  = new byte[ciphertext.Length];

                using var aes = new AesGcm(_encryptionKey, AesGcm.TagByteSizes.MaxSize);
                aes.Decrypt(nonce, ciphertext, tag, plaintext);
                return Encoding.UTF8.GetString(plaintext);
            }
            catch
            {
                throw new Exception(
                    "The stored API token can no longer be decrypted (key mismatch or token corruption). " +
                    "Please reconfigure the Jira integration with a fresh API token.");
            }
        }

        // ============================================================================
        // Admin: Configuration Management
        // ============================================================================

        public async Task<JiraIntegrationResponseDTO> ConfigureIntegrationAsync(int adminUserId, int projectId, ConfigureJiraIntegrationDTO dto)
        {
            var user = await ValidateUserAsync(adminUserId);
            ValidateAdminRole(user);

            // Validate project exists
            var project = await _projectRepo.GetByIdAsync(projectId);
            if (project == null)
            {
                throw new Exception("Project not found");
            }

            // Test connection before saving
            var isConnected = await _jiraApiService.TestConnectionAsync(dto.JiraUrl, dto.JiraEmail, dto.ApiToken);
            if (!isConnected)
            {
                throw new Exception("Failed to connect to Jira. Please check your credentials and URL.");
            }

            // Verify project exists in Jira
            JiraProjectDTO jiraProject;
            try
            {
                jiraProject = await _jiraApiService.GetProjectAsync(dto.JiraUrl, dto.JiraEmail, dto.ApiToken, dto.ProjectKey);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get Jira project: {ex.Message}");
            }

            // Check if integration already exists
            var existingIntegration = await _jiraIntegrationRepo.GetByProjectIdAsync(projectId);
            if (existingIntegration != null)
            {
                throw new Exception("Jira integration already exists for this project. Use update endpoint instead.");
            }

            // Create new integration
            var integration = new JiraIntegration
            {
                ProjectId = projectId,
                JiraUrl = dto.JiraUrl.TrimEnd('/'),
                JiraEmail = dto.JiraEmail,
                ApiToken = EncryptToken(dto.ApiToken),
                ProjectKey = dto.ProjectKey,
                SyncStatus = SyncStatus.pending
            };

            await _jiraIntegrationRepo.AddAsync(integration);

            return new JiraIntegrationResponseDTO
            {
                IntegrationId = integration.IntegrationId,
                ProjectId = integration.ProjectId,
                ProjectName = project.ProjectName,
                JiraUrl = integration.JiraUrl,
                JiraEmail = integration.JiraEmail,
                ProjectKey = integration.ProjectKey,
                LastSync = integration.LastSync,
                SyncStatus = integration.SyncStatus.ToString(),
                CreatedAt = integration.CreatedAt,
                UpdatedAt = integration.UpdatedAt
            };
        }

        public async Task<JiraIntegrationResponseDTO?> GetIntegrationAsync(int userId, int projectId)
        {
            var user = await ValidateUserAsync(userId);

            // Only admin, lecturer, or project members can view integration
            if (user.Role != UserRole.admin &&
                !await IsUserProjectLecturerAsync(userId, projectId) &&
                !await IsUserProjectMemberAsync(userId, projectId))
            {
                throw new UnauthorizedAccessException("You don't have permission to view this integration");
            }

            var integration = await _jiraIntegrationRepo.GetByProjectIdAsync(projectId);
            if (integration == null) return null;

            return new JiraIntegrationResponseDTO
            {
                IntegrationId = integration.IntegrationId,
                ProjectId = integration.ProjectId,
                ProjectName = integration.Project.ProjectName,
                JiraUrl = integration.JiraUrl,
                JiraEmail = integration.JiraEmail,
                ProjectKey = integration.ProjectKey,
                LastSync = integration.LastSync,
                SyncStatus = integration.SyncStatus.ToString(),
                CreatedAt = integration.CreatedAt,
                UpdatedAt = integration.UpdatedAt
            };
        }

        public async Task<JiraIntegrationResponseDTO> UpdateIntegrationAsync(int adminUserId, int projectId, ConfigureJiraIntegrationDTO dto)
        {
            var user = await ValidateUserAsync(adminUserId);
            ValidateAdminRole(user);

            var integration = await _jiraIntegrationRepo.GetByProjectIdAsync(projectId);
            if (integration == null)
            {
                throw new Exception("Jira integration not found for this project");
            }

            // Test new connection
            var isConnected = await _jiraApiService.TestConnectionAsync(dto.JiraUrl, dto.JiraEmail, dto.ApiToken);
            if (!isConnected)
            {
                throw new Exception("Failed to connect to Jira with new credentials");
            }

            // Update integration
            integration.JiraUrl = dto.JiraUrl.TrimEnd('/');
            integration.JiraEmail = dto.JiraEmail;
            integration.ApiToken = EncryptToken(dto.ApiToken);
            integration.ProjectKey = dto.ProjectKey;

            await _jiraIntegrationRepo.UpdateAsync(integration);

            return new JiraIntegrationResponseDTO
            {
                IntegrationId = integration.IntegrationId,
                ProjectId = integration.ProjectId,
                ProjectName = integration.Project.ProjectName,
                JiraUrl = integration.JiraUrl,
                JiraEmail = integration.JiraEmail,
                ProjectKey = integration.ProjectKey,
                LastSync = integration.LastSync,
                SyncStatus = integration.SyncStatus.ToString(),
                CreatedAt = integration.CreatedAt,
                UpdatedAt = integration.UpdatedAt
            };
        }

        public async System.Threading.Tasks.Task DeleteIntegrationAsync(int adminUserId, int projectId)
        {
            var user = await ValidateUserAsync(adminUserId);
            ValidateAdminRole(user);

            var integration = await _jiraIntegrationRepo.GetByProjectIdAsync(projectId);
            if (integration == null)
            {
                throw new Exception("Jira integration not found");
            }

            await _jiraIntegrationRepo.DeleteAsync(integration.IntegrationId);
        }

        public async Task<JiraConnectionTestDTO> TestConnectionAsync(int adminUserId, int projectId)
        {
            var user = await ValidateUserAsync(adminUserId);
            ValidateAdminRole(user);

            var integration = await _jiraIntegrationRepo.GetByProjectIdAsync(projectId);
            if (integration == null)
            {
                throw new Exception("Jira integration not found");
            }

            var decryptedToken = DecryptToken(integration.ApiToken);
            var isConnected = await _jiraApiService.TestConnectionAsync(integration.JiraUrl, integration.JiraEmail, decryptedToken);

            string message;
            string? projectName = null;
            string? projectKey = null;

            if (isConnected)
            {
                try
                {
                    var jiraProject = await _jiraApiService.GetProjectAsync(
                        integration.JiraUrl, integration.JiraEmail, decryptedToken, integration.ProjectKey);
                    projectName = jiraProject.Name;
                    projectKey = jiraProject.Key;
                    message = $"Successfully connected to Jira project: {projectName}";
                }
                catch (Exception ex)
                {
                    message = $"Connected to Jira but failed to get project: {ex.Message}";
                    isConnected = false;
                }
            }
            else
            {
                message = "Failed to connect to Jira. Please check your credentials.";
            }

            return new JiraConnectionTestDTO
            {
                IsConnected = isConnected,
                Message = message,
                JiraProjectName = projectName,
                JiraProjectKey = projectKey,
                TestedAt = DateTime.UtcNow
            };
        }

        public async Task<List<JiraIntegrationResponseDTO>> GetAllIntegrationsAsync(int adminUserId)
        {
            var user = await ValidateUserAsync(adminUserId);
            ValidateAdminRole(user);

            var integrations = await _jiraIntegrationRepo.GetAllAsync();

            return integrations.Select(i => new JiraIntegrationResponseDTO
            {
                IntegrationId = i.IntegrationId,
                ProjectId = i.ProjectId,
                ProjectName = i.Project.ProjectName,
                JiraUrl = i.JiraUrl,
                JiraEmail = i.JiraEmail,
                ProjectKey = i.ProjectKey,
                LastSync = i.LastSync,
                SyncStatus = i.SyncStatus.ToString(),
                CreatedAt = i.CreatedAt,
                UpdatedAt = i.UpdatedAt
            }).ToList();
        }

        // ============================================================================
        // Synchronization
        // ============================================================================

        public async Task<JiraSyncResultDTO> SyncIssuesAsync(int userId, int projectId)
        {
            var user = await ValidateUserAsync(userId);

            // Only admin or project leader can sync
            if (user.Role != UserRole.admin && !await IsUserProjectLeaderAsync(userId, projectId))
            {
                throw new UnauthorizedAccessException("Only administrators or project leaders can sync issues");
            }

            var integration = await _jiraIntegrationRepo.GetByProjectIdAsync(projectId);
            if (integration == null)
            {
                throw new Exception("Jira integration not configured for this project");
            }

            var result = new JiraSyncResultDTO
            {
                SyncTime = DateTime.UtcNow,
                Status = "success"
            };

            try
            {
                // Update sync status
                integration.SyncStatus = SyncStatus.syncing;
                await _jiraIntegrationRepo.UpdateAsync(integration);

                var decryptedToken = DecryptToken(integration.ApiToken);
                var jiraIssues = await _jiraApiService.GetProjectIssuesAsync(
                    integration.JiraUrl, integration.JiraEmail, decryptedToken, integration.ProjectKey);

                result.TotalIssues = jiraIssues.Count;

                foreach (var jiraIssue in jiraIssues)
                {
                    try
                    {
                        var existingIssue = await _jiraIssueRepo.GetByJiraIdAsync(jiraIssue.JiraId);

                        if (existingIssue == null)
                        {
                            // Create new issue
                            var newIssue = new JiraIssue
                            {
                                ProjectId = projectId,
                                IssueKey = jiraIssue.IssueKey,
                                JiraId = jiraIssue.JiraId,
                                IssueType = jiraIssue.IssueType,
                                Summary = jiraIssue.Summary,
                                Description = jiraIssue.Description,
                                Priority = !string.IsNullOrEmpty(jiraIssue.Priority)
                                    ? Enum.Parse<JiraPriority>(jiraIssue.Priority.ToLower())
                                    : (JiraPriority?)null,
                                Status = jiraIssue.Status,
                                AssigneeJiraId = jiraIssue.AssigneeJiraId,
                                SprintId = jiraIssue.SprintId,
                                SprintName = jiraIssue.SprintName,
                                SprintState = jiraIssue.SprintState,
                                CreatedDate = jiraIssue.CreatedDate,
                                UpdatedDate = jiraIssue.UpdatedDate
                            };

                            await _jiraIssueRepo.AddAsync(newIssue);
                            result.NewIssues++;
                        }
                        else
                        {
                            var previousProjectId = existingIssue.ProjectId;

                            // Keep issue ownership aligned with the project currently being synced.
                            // This prevents "updated but not visible in this project" confusion when
                            // legacy rows were previously attached to another project.
                            if (existingIssue.ProjectId != projectId)
                            {
                                existingIssue.ProjectId = projectId;
                                result.Warnings.Add(
                                    $"Issue {jiraIssue.IssueKey} was reassigned from project {previousProjectId} to {projectId} during sync.");
                            }

                            // Update existing issue
                            existingIssue.IssueKey = jiraIssue.IssueKey;
                            existingIssue.JiraId = jiraIssue.JiraId;
                            existingIssue.IssueType = jiraIssue.IssueType;
                            existingIssue.Summary = jiraIssue.Summary;
                            existingIssue.Description = jiraIssue.Description;
                            existingIssue.Status = jiraIssue.Status;
                            existingIssue.Priority = !string.IsNullOrEmpty(jiraIssue.Priority)
                                ? Enum.Parse<JiraPriority>(jiraIssue.Priority.ToLower())
                                : (JiraPriority?)null;
                            existingIssue.AssigneeJiraId = jiraIssue.AssigneeJiraId;
                            existingIssue.SprintId = jiraIssue.SprintId;
                            existingIssue.SprintName = jiraIssue.SprintName;
                            existingIssue.SprintState = jiraIssue.SprintState;
                            existingIssue.UpdatedDate = jiraIssue.UpdatedDate;

                            await _jiraIssueRepo.UpdateAsync(existingIssue);
                            result.UpdatedIssues++;
                        }
                    }
                    catch (Exception ex)
                    {
                        result.FailedIssues++;
                        result.Errors.Add($"Failed to sync issue {jiraIssue.IssueKey}: {ex.Message}");
                    }
                }

                // Update integration
                integration.LastSync = DateTime.UtcNow;
                integration.SyncStatus = SyncStatus.success;
                await _jiraIntegrationRepo.UpdateAsync(integration);
            }
            catch (Exception ex)
            {
                integration.SyncStatus = SyncStatus.failed;
                await _jiraIntegrationRepo.UpdateAsync(integration);

                result.Status = "failed";
                result.Errors.Add($"Sync failed: {ex.Message}");
            }

            return result;
        }

        public async Task<JiraSyncResultDTO> GetSyncStatusAsync(int userId, int projectId)
        {
            var user = await ValidateUserAsync(userId);

            // Validate access
            if (user.Role != UserRole.admin &&
                !await IsUserProjectLecturerAsync(userId, projectId) &&
                !await IsUserProjectMemberAsync(userId, projectId))
            {
                throw new UnauthorizedAccessException("You don't have permission to view sync status");
            }

            var integration = await _jiraIntegrationRepo.GetByProjectIdAsync(projectId);
            if (integration == null)
            {
                throw new Exception("Jira integration not configured");
            }

            var issues = await _jiraIssueRepo.GetByProjectIdAsync(projectId);

            return new JiraSyncResultDTO
            {
                TotalIssues = issues.Count,
                SyncTime = integration.LastSync ?? DateTime.MinValue,
                Status = integration.SyncStatus.ToString()
            };
        }

        // ============================================================================
        // Issue Viewing (Role-based)
        // ============================================================================

        public async Task<List<JiraIssueDTO>> GetProjectIssuesAsync(int userId, int projectId)
        {
            var user = await ValidateUserAsync(userId);

            // Validate access
            var hasAccess = user.Role == UserRole.admin ||
                           await IsUserProjectLecturerAsync(userId, projectId) ||
                           await IsUserProjectLeaderAsync(userId, projectId) ||
                           await IsUserProjectMemberAsync(userId, projectId);

            if (!hasAccess)
            {
                throw new UnauthorizedAccessException("You don't have permission to view these issues");
            }

            var issues = await _jiraIssueRepo.GetByProjectIdAsync(projectId);

            // If student (not leader), filter to assigned issues only
            if (user.Role == UserRole.student && !await IsUserProjectLeaderAsync(userId, projectId))
            {
                var userJiraId = user.JiraAccountId;
                issues = issues.Where(i => i.AssigneeJiraId == userJiraId).ToList();
            }

            return issues.Select(i => new JiraIssueDTO
            {
                JiraIssueId = i.JiraIssueId,
                IssueKey = i.IssueKey,
                JiraId = i.JiraId,
                IssueType = i.IssueType,
                Summary = i.Summary,
                Description = i.Description,
                Priority = i.Priority?.ToString(),
                Status = i.Status,
                AssigneeJiraId = i.AssigneeJiraId,
                SprintId = i.SprintId,
                SprintName = i.SprintName,
                SprintState = i.SprintState,
                CreatedDate = i.CreatedDate,
                UpdatedDate = i.UpdatedDate,
                LastSynced = i.LastSynced
            }).ToList();
        }

        public async Task<JiraIssueDTO> GetIssueDetailsAsync(int userId, string issueKey)
        {
            var user = await ValidateUserAsync(userId);

            var issue = await _jiraIssueRepo.GetByIssueKeyAsync(issueKey);
            if (issue == null)
            {
                throw new Exception("Issue not found");
            }

            // Validate access to project
            var hasAccess = user.Role == UserRole.admin ||
                           await IsUserProjectLecturerAsync(userId, issue.ProjectId) ||
                           await IsUserProjectLeaderAsync(userId, issue.ProjectId) ||
                           await IsUserProjectMemberAsync(userId, issue.ProjectId);

            if (!hasAccess)
            {
                throw new UnauthorizedAccessException("You don't have permission to view this issue");
            }

            // If student, only show if assigned to them
            if (user.Role == UserRole.student && !await IsUserProjectLeaderAsync(userId, issue.ProjectId))
            {
                if (issue.AssigneeJiraId != user.JiraAccountId)
                {
                    throw new UnauthorizedAccessException("You can only view issues assigned to you");
                }
            }

            return new JiraIssueDTO
            {
                JiraIssueId = issue.JiraIssueId,
                IssueKey = issue.IssueKey,
                JiraId = issue.JiraId,
                IssueType = issue.IssueType,
                Summary = issue.Summary,
                Description = issue.Description,
                Priority = issue.Priority?.ToString(),
                Status = issue.Status,
                AssigneeJiraId = issue.AssigneeJiraId,
                SprintId = issue.SprintId,
                SprintName = issue.SprintName,
                SprintState = issue.SprintState,
                CreatedDate = issue.CreatedDate,
                UpdatedDate = issue.UpdatedDate,
                LastSynced = issue.LastSynced
            };
        }

        // ============================================================================
        // Group-based wrappers (resolve groupId → projectId internally)
        // ============================================================================

        private async Task<int> ResolveProjectIdFromGroupAsync(int groupId)
        {
            var project = await _projectRepo.GetByGroupIdAsync(groupId);
            if (project == null)
                throw new Exception($"No project found for group {groupId}");
            return project.ProjectId;
        }

        public async Task<JiraSyncResultDTO> SyncIssuesByGroupAsync(int userId, int groupId)
        {
            var projectId = await ResolveProjectIdFromGroupAsync(groupId);
            return await SyncIssuesAsync(userId, projectId);
        }

        public async Task<JiraSyncResultDTO> GetSyncStatusByGroupAsync(int userId, int groupId)
        {
            var projectId = await ResolveProjectIdFromGroupAsync(groupId);
            return await GetSyncStatusAsync(userId, projectId);
        }

        public async Task<List<JiraIssueDTO>> GetProjectIssuesByGroupAsync(int userId, int groupId)
        {
            var projectId = await ResolveProjectIdFromGroupAsync(groupId);
            return await GetProjectIssuesAsync(userId, projectId);
        }
    }
}






