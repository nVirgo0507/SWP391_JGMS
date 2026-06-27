using BLL.DTOs.Jira;
using BLL.Services.Interface;
using DAL.Models;
using DAL.Repositories.Interface;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Task = System.Threading.Tasks.Task;

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
        private readonly IRequirementRepository _requirementRepo;
        private readonly IProjectRepository _projectRepo;
        private readonly IUserRepository _userRepo;
        private readonly IStudentGroupRepository _groupRepo;
        private readonly IGroupMemberRepository _groupMemberRepo;
        private readonly IJiraApiService _jiraApiService;
        private readonly ITokenEncryptionService _tokenEncryption;
        private readonly IAtlassianAuthService _atlassianAuth;
        private readonly ITaskRepository _taskRepo;
        private readonly IPersonalTaskStatisticRepository _statRepo;

        public JiraIntegrationService(
            IJiraIntegrationRepository jiraIntegrationRepo,
            IJiraIssueRepository jiraIssueRepo,
            IRequirementRepository requirementRepo,
            IProjectRepository projectRepo,
            IUserRepository userRepo,
            IStudentGroupRepository groupRepo,
            IGroupMemberRepository groupMemberRepo,
            IJiraApiService jiraApiService,
            ITokenEncryptionService tokenEncryption,
            IAtlassianAuthService atlassianAuth,
            ITaskRepository taskRepo,
            IPersonalTaskStatisticRepository statRepo)
        {
            _jiraIntegrationRepo = jiraIntegrationRepo;
            _jiraIssueRepo = jiraIssueRepo;
            _requirementRepo = requirementRepo;
            _projectRepo = projectRepo;
            _userRepo = userRepo;
            _groupRepo = groupRepo;
            _groupMemberRepo = groupMemberRepo;
            _jiraApiService = jiraApiService;
            _tokenEncryption = tokenEncryption;
            _atlassianAuth = atlassianAuth;
            _taskRepo = taskRepo;
            _statRepo = statRepo;
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

        private async Task ValidateAdminOrLeaderAsync(User user, int projectId)
        {
            if (user.Role == UserRole.admin) return;
            if (await IsUserProjectLeaderAsync(user.UserId, projectId)) return;
            throw new UnauthorizedAccessException("Only Admins and Team Leaders can configure Jira integration.");
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

        private string EncryptToken(string token) => _tokenEncryption.Encrypt(token);

        private string DecryptToken(string encryptedToken)
        {
            try
            {
                return _tokenEncryption.Decrypt(encryptedToken);
            }
            catch
            {
                throw new Exception(
                    "The stored API token can no longer be decrypted (key mismatch or token corruption). " +
                    "Please reconfigure the Jira integration with a fresh API token.");
            }
        }

        private async System.Threading.Tasks.Task SyncLinkedRequirementFromIssueAsync(JiraIssue issue)
        {
            var requirement = await _requirementRepo.GetByJiraIssueIdAsync(issue.JiraIssueId);
            if (requirement == null)
            {
                return;
            }

            requirement.Title = issue.Summary;
            requirement.Description = issue.Description;

            if (issue.Priority.HasValue)
            {
                requirement.Priority = MapJiraPriorityToRequirementPriority(issue.Priority.Value);
            }

            // Keep the requirement in the same project as the synced Jira issue.
            if (requirement.ProjectId != issue.ProjectId)
            {
                requirement.ProjectId = issue.ProjectId;
            }

            await _requirementRepo.UpdateAsync(requirement);
        }

        private static PriorityLevel MapJiraPriorityToRequirementPriority(JiraPriority jiraPriority) =>
            jiraPriority switch
            {
                JiraPriority.highest or JiraPriority.high => PriorityLevel.high,
                JiraPriority.low or JiraPriority.lowest => PriorityLevel.low,
                _ => PriorityLevel.medium
            };

        // ============================================================================
        // Admin: Configuration Management
        // ============================================================================

        public async Task<JiraIntegrationResponseDTO> ConfigureIntegrationAsync(int adminUserId, int projectId, ConfigureJiraIntegrationDTO dto)
        {
            var user = await ValidateUserAsync(adminUserId);
            await ValidateAdminOrLeaderAsync(user, projectId);

            // Validate project exists
            var project = await _projectRepo.GetByIdAsync(projectId);
            if (project == null)
            {
                throw new Exception("Project not found");
            }

            // BR-023: Validate Jira Credentials
            // Test connection before saving
            var isConnected = await _jiraApiService.TestConnectionAsync(dto.CloudId, "oauth@placeholder.com", dto.AccessToken);
            if (!isConnected)
            {
                throw new Exception("Invalid Jira credentials. Please check your API token and email.");
            }

            // Verify project exists in Jira
            JiraProjectDTO jiraProject;
            try
            {
                jiraProject = await _jiraApiService.GetProjectAsync(dto.CloudId, "oauth@placeholder.com", dto.AccessToken, dto.ProjectKey);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get Jira project: {ex.Message}");
            }

            // BR-020: One Jira Integration Per Project
            var existingIntegration = await _jiraIntegrationRepo.GetByProjectIdAsync(projectId);
            if (existingIntegration != null)
            {
                throw new Exception("Jira integration already configured for this project");
            }

            // Create new integration
            var integration = new JiraIntegration
            {
                ProjectId = projectId,
                JiraUrl = "oauth-placeholder",
                JiraEmail = "oauth-placeholder",
                ApiToken = "oauth-placeholder",
                CloudId = dto.CloudId,
                AccessToken = EncryptToken(dto.AccessToken),
                RefreshToken = string.IsNullOrEmpty(dto.RefreshToken) ? null : EncryptToken(dto.RefreshToken),
                TokenExpiresAt = dto.ExpiresIn.HasValue ? DateTime.UtcNow.AddSeconds(dto.ExpiresIn.Value) : null,
                ProjectKey = dto.ProjectKey,
                SyncStatus = SyncStatus.pending
            };

            await _jiraIntegrationRepo.AddAsync(integration);

            return new JiraIntegrationResponseDTO
            {
                IntegrationId = integration.IntegrationId,
                ProjectId = integration.ProjectId,
                ProjectName = project.ProjectName,
                JiraUrl = integration.ActiveUrl,
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
                JiraUrl = integration.ActiveUrl,
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
            await ValidateAdminOrLeaderAsync(user, projectId);

            var integration = await _jiraIntegrationRepo.GetByProjectIdAsync(projectId);
            if (integration == null)
            {
                throw new Exception("Jira integration not found for this project");
            }

            // Test new connection
            var isConnected = await _jiraApiService.TestConnectionAsync(dto.CloudId, "oauth@placeholder.com", dto.AccessToken);
            if (!isConnected)
            {
                throw new Exception("Invalid Jira credentials. Please check your API token and email.");
            }

            // Update integration
            integration.CloudId = dto.CloudId;
            integration.AccessToken = EncryptToken(dto.AccessToken);
            integration.RefreshToken = string.IsNullOrEmpty(dto.RefreshToken) ? null : EncryptToken(dto.RefreshToken);
            integration.TokenExpiresAt = dto.ExpiresIn.HasValue ? DateTime.UtcNow.AddSeconds(dto.ExpiresIn.Value) : null;
            integration.ProjectKey = dto.ProjectKey;

            await _jiraIntegrationRepo.UpdateAsync(integration);

            return new JiraIntegrationResponseDTO
            {
                IntegrationId = integration.IntegrationId,
                ProjectId = integration.ProjectId,
                ProjectName = integration.Project.ProjectName,
                JiraUrl = integration.ActiveUrl,
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

            // Refresh token if expired
            if (!string.IsNullOrEmpty(integration.CloudId) && 
                !string.IsNullOrEmpty(integration.RefreshToken) && 
                integration.TokenExpiresAt.HasValue && 
                integration.TokenExpiresAt.Value <= DateTime.UtcNow.AddMinutes(5))
            {
                var refreshResult = await _atlassianAuth.RefreshTokensAsync(DecryptToken(integration.RefreshToken));
                integration.AccessToken = EncryptToken(refreshResult.AccessToken);
                if (!string.IsNullOrEmpty(refreshResult.RefreshToken))
                {
                    integration.RefreshToken = EncryptToken(refreshResult.RefreshToken);
                }
                integration.TokenExpiresAt = DateTime.UtcNow.AddSeconds(refreshResult.ExpiresIn);
                await _jiraIntegrationRepo.UpdateAsync(integration);
            }

            string url = !string.IsNullOrEmpty(integration.CloudId) 
                ? $"https://api.atlassian.com/ex/jira/{integration.CloudId}" 
                : integration.ActiveUrl;
            string token = !string.IsNullOrEmpty(integration.AccessToken) ? DecryptToken(integration.AccessToken) : DecryptToken(integration.ApiToken);

            var isConnected = await _jiraApiService.TestConnectionAsync(url, "oauth@placeholder.com", token);

            string message;
            string? projectName = null;
            string? projectKey = null;

            if (isConnected)
            {
                try
                {
                    var jiraProject = await _jiraApiService.GetProjectAsync(
                        url, "oauth@placeholder.com", token, integration.ProjectKey);
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
                message = "Invalid Jira credentials. Please check your API token and email.";
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

            // BR-025: Sync Interval Limits (at least 5 minutes)
            // Error Message: "Please wait at least 5 minutes between manual syncs"
            if (integration.LastSync.HasValue && (DateTime.UtcNow - integration.LastSync.Value).TotalMinutes < 5)
            {
                throw new Exception("Please wait at least 5 minutes between manual syncs");
            }

            var result = new JiraSyncResultDTO
            {
                SyncTime = DateTime.UtcNow,
                Status = "success"
            };

            try
            {
                // Refresh token if expired
                if (!string.IsNullOrEmpty(integration.CloudId) && 
                    !string.IsNullOrEmpty(integration.RefreshToken) && 
                    integration.TokenExpiresAt.HasValue && 
                    integration.TokenExpiresAt.Value <= DateTime.UtcNow.AddMinutes(5))
                {
                    var refreshResult = await _atlassianAuth.RefreshTokensAsync(DecryptToken(integration.RefreshToken));
                    integration.AccessToken = EncryptToken(refreshResult.AccessToken);
                    if (!string.IsNullOrEmpty(refreshResult.RefreshToken))
                    {
                        integration.RefreshToken = EncryptToken(refreshResult.RefreshToken);
                    }
                    integration.TokenExpiresAt = DateTime.UtcNow.AddSeconds(refreshResult.ExpiresIn);
                }

                // Update sync status
                integration.SyncStatus = SyncStatus.syncing;
                await _jiraIntegrationRepo.UpdateAsync(integration);

                string url = !string.IsNullOrEmpty(integration.CloudId) 
                    ? $"https://api.atlassian.com/ex/jira/{integration.CloudId}" 
                    : integration.ActiveUrl;
                string token = !string.IsNullOrEmpty(integration.AccessToken) ? DecryptToken(integration.AccessToken) : DecryptToken(integration.ApiToken);

                var jiraIssues = await _jiraApiService.GetProjectIssuesAsync(
                    url, "oauth@placeholder.com", token, integration.ProjectKey);

                result.TotalIssues = jiraIssues.Count;

                var issuesToAdd = new List<JiraIssue>();
                var issuesToUpdate = new List<JiraIssue>();
                var requirementsToUpdate = new List<Requirement>();

                // BULK LOAD to eliminate N+1 queries
                var existingIssuesList = await _jiraIssueRepo.GetByProjectIdAsync(projectId);
                var existingIssuesDict = existingIssuesList
                    .GroupBy(i => i.JiraId)
                    .ToDictionary(g => g.Key, g => g.First());
                
                var existingRequirementsList = await _requirementRepo.GetByProjectIdAsync(projectId);
                var requirementsDict = existingRequirementsList
                    .Where(r => r.JiraIssueId.HasValue)
                    .GroupBy(r => r.JiraIssueId.Value)
                    .ToDictionary(g => g.Key, g => g.First());

                
                var existingTasksList = await _taskRepo.GetTasksByProjectIdAsync(projectId);
                var tasksDict = existingTasksList
                    .Where(t => t.JiraIssueId.HasValue)
                    .GroupBy(t => t.JiraIssueId!.Value)
                    .ToDictionary(g => g.Key, g => g.First());
                var tasksToUpdate = new HashSet<DAL.Models.Task>();
                
                var allUsers = await _userRepo.GetAllAsync();
                var usersByJiraId = allUsers
                    .Where(u => !string.IsNullOrEmpty(u.JiraAccountId))
                    .GroupBy(u => u.JiraAccountId!)
                    .ToDictionary(g => g.Key, g => g.First());
                var usersToRecalculateStats = new HashSet<int>();

                foreach (var jiraIssue in jiraIssues)
                {
                    try
                    {
                        if (!existingIssuesDict.TryGetValue(jiraIssue.JiraId, out var existingIssue))
                        {
                            var newIssue = new JiraIssue
                            {
                                ProjectId = projectId,
                                IssueKey = jiraIssue.IssueKey,
                                JiraId = jiraIssue.JiraId,
                                IssueType = jiraIssue.IssueType,
                                Summary = jiraIssue.Summary,
                                Description = jiraIssue.Description,
                                Priority = !string.IsNullOrEmpty(jiraIssue.Priority) && Enum.TryParse<JiraPriority>(jiraIssue.Priority.ToLower(), out var p1) ? p1 : (JiraPriority?)null,
                                Status = jiraIssue.Status,
                                AssigneeJiraId = jiraIssue.AssigneeJiraId,
                                SprintId = jiraIssue.SprintId,
                                SprintName = jiraIssue.SprintName,
                                SprintState = jiraIssue.SprintState,
                                CreatedDate = jiraIssue.CreatedDate,
                                UpdatedDate = jiraIssue.UpdatedDate
                            };

                            issuesToAdd.Add(newIssue);
                            result.NewIssues++;
                        }
                        else
                        {
                            var previousProjectId = existingIssue.ProjectId;

                            if (existingIssue.ProjectId != projectId)
                            {
                                existingIssue.ProjectId = projectId;
                                result.Warnings.Add($"Issue {jiraIssue.IssueKey} was reassigned from project {previousProjectId} to {projectId} during sync.");
                            }

                            existingIssue.IssueKey = jiraIssue.IssueKey;
                            existingIssue.JiraId = jiraIssue.JiraId;
                            existingIssue.IssueType = jiraIssue.IssueType;
                            existingIssue.Summary = jiraIssue.Summary;
                            existingIssue.Description = jiraIssue.Description;
                            existingIssue.Status = jiraIssue.Status;
                            existingIssue.Priority = !string.IsNullOrEmpty(jiraIssue.Priority) && Enum.TryParse<JiraPriority>(jiraIssue.Priority.ToLower(), out var p2) ? p2 : (JiraPriority?)null;
                            existingIssue.AssigneeJiraId = jiraIssue.AssigneeJiraId;
                            existingIssue.SprintId = jiraIssue.SprintId;
                            existingIssue.SprintName = jiraIssue.SprintName;
                            existingIssue.SprintState = jiraIssue.SprintState;
                            existingIssue.UpdatedDate = jiraIssue.UpdatedDate;

                            issuesToUpdate.Add(existingIssue);
                            result.UpdatedIssues++;

                            if (requirementsDict.TryGetValue(existingIssue.JiraIssueId, out var requirement))
                            {
                                requirement.Title = existingIssue.Summary;
                                requirement.Description = existingIssue.Description;
                                if (existingIssue.Priority.HasValue)
                                    requirement.Priority = MapJiraPriorityToRequirementPriority(existingIssue.Priority.Value);
                                if (requirement.ProjectId != existingIssue.ProjectId)
                                    requirement.ProjectId = existingIssue.ProjectId;
                                
                                requirementsToUpdate.Add(requirement);
                            }
                            if (tasksDict.TryGetValue(existingIssue.JiraIssueId, out var task))
                            {
                                bool taskChanged = false;
                                
                                // Auto-sync Assignee from Jira to JGMS
                                if (!string.IsNullOrEmpty(existingIssue.AssigneeJiraId) && 
                                    usersByJiraId.TryGetValue(existingIssue.AssigneeJiraId, out var mappedUser))
                                {
                                    if (task.AssignedTo != mappedUser.UserId)
                                    {
                                        if (task.AssignedTo.HasValue) usersToRecalculateStats.Add(task.AssignedTo.Value);
                                        task.AssignedTo = mappedUser.UserId;
                                        usersToRecalculateStats.Add(mappedUser.UserId);
                                        taskChanged = true;
                                    }
                                }
                                else if (string.IsNullOrEmpty(existingIssue.AssigneeJiraId) && task.AssignedTo.HasValue)
                                {
                                    usersToRecalculateStats.Add(task.AssignedTo.Value);
                                    task.AssignedTo = null;
                                    taskChanged = true;
                                }

                                if (task.Title != existingIssue.Summary) { task.Title = existingIssue.Summary; taskChanged = true; }
                                if (task.Description != existingIssue.Description) { task.Description = existingIssue.Description; taskChanged = true; }
                                
                                var newStatus = existingIssue.Status?.ToLower() switch
                                {
                                    "to do" or "todo" or "to_do" => DAL.Models.TaskStatus.todo,
                                    "in progress" or "in_progress" => DAL.Models.TaskStatus.in_progress,
                                    "done" or "completed" => DAL.Models.TaskStatus.done,
                                    _ => DAL.Models.TaskStatus.todo
                                };
                                if (task.Status != newStatus) 
                                { 
                                    task.Status = newStatus; 
                                    if (newStatus == DAL.Models.TaskStatus.done && task.CompletedAt == null) task.CompletedAt = DateTime.UtcNow;
                                    else if (newStatus != DAL.Models.TaskStatus.done) task.CompletedAt = null;
                                    taskChanged = true; 
                                }

                                if (existingIssue.Priority.HasValue)
                                {
                                    var newPriority = MapJiraPriorityToRequirementPriority(existingIssue.Priority.Value);
                                    if (task.Priority != newPriority) { task.Priority = newPriority; taskChanged = true; }
                                }

                                if (taskChanged)
                                {
                                    tasksToUpdate.Add(task);
                                    if (task.AssignedTo.HasValue) usersToRecalculateStats.Add(task.AssignedTo.Value);
                                }
                            }

                        }
                    }
                    catch (Exception ex)
                    {
                        result.FailedIssues++;
                        result.Errors.Add($"Failed to process issue {jiraIssue.IssueKey}: {ex.Message}");
                    }
                }

                if (issuesToAdd.Any()) await _jiraIssueRepo.AddRangeAsync(issuesToAdd);
                if (issuesToUpdate.Any()) await _jiraIssueRepo.UpdateRangeAsync(issuesToUpdate);
                if (requirementsToUpdate.Any()) await _requirementRepo.UpdateRangeAsync(requirementsToUpdate);
                if (tasksToUpdate.Any()) foreach(var t in tasksToUpdate) { await _taskRepo.UpdateAsync(t); }
                foreach(var uId in usersToRecalculateStats)
                {
                    await _statRepo.RecalculateForUserProjectAsync(uId, projectId);
                }


                // Update integration
                integration.LastSync = DateTime.UtcNow;
                integration.SyncStatus = SyncStatus.success;
                await _jiraIntegrationRepo.UpdateAsync(integration);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n\n[JIRA SYNC ERROR] {ex.ToString()}\n\n");
                integration.SyncStatus = SyncStatus.failed;
                await _jiraIntegrationRepo.UpdateAsync(integration);
                throw new Exception($"Jira sync failed: {ex.Message}");
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

            // Fetch all users in this project to map AssigneeNames efficiently
            var projectUsers = await _userRepo.GetAllAsync(); // Or a more specific project-member lookup
            var userMap = projectUsers
                .Where(u => !string.IsNullOrEmpty(u.JiraAccountId))
                .ToDictionary(u => u.JiraAccountId!, u => u.FullName);

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
                AssigneeName = !string.IsNullOrEmpty(i.AssigneeJiraId) && userMap.TryGetValue(i.AssigneeJiraId, out var name) 
                    ? name 
                    : null,
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

            string? assigneeName = null;
            if (!string.IsNullOrEmpty(issue.AssigneeJiraId))
            {
                var assignee = await _userRepo.GetByJiraAccountIdAsync(issue.AssigneeJiraId);
                assigneeName = assignee?.FullName;
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
                AssigneeName = assigneeName,
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






