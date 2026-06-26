using BLL.DTOs.Admin;
using BLL.DTOs.Jira;
using BLL.Helpers;
using BLL.Services.Interface;
using DAL.Models;
using DAL.Repositories.Interface;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Task = System.Threading.Tasks.Task;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using BLL.Helpers;

namespace BLL.Services
{
    /// <summary>
    /// Service for team leader-scoped operations with group access control
    /// BR-055: Team Leader Group-Scoped Access - Team leaders can only manage their own group's project
    /// Validation: Check user is leader of the group via GROUP_MEMBER.is_leader
    /// Error Message: "Access denied. You are not the leader of this group."
    /// </summary>
    public class TeamLeaderRequirementService : ITeamLeaderRequirementService
    {
        private readonly IGroupMemberRepository _memberRepository;
        private readonly ILeaderValidationService _leaderValidationService;
        private readonly IStudentGroupRepository _groupRepository;
        private readonly IUserRepository _userRepository;
        private readonly ITaskRepository _taskRepository;
        private readonly IJiraIssueRepository _jiraIssueRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly ICommitRepository _commitRepository;
        private readonly ICommitStatisticRepository _commitStatisticRepository;
        private readonly IRequirementRepository _requirementRepository;
        private readonly IJiraIntegrationRepository _jiraIntegrationRepository;
        private readonly IJiraApiService _jiraApiService;
        private readonly IPersonalTaskStatisticRepository _personalTaskStatisticRepository;
        private readonly byte[] _encryptionKey;
        private readonly ISrsDocumentRepository _srsDocumentRepository;
        private readonly IAiChatService _aiChatService;

        public TeamLeaderRequirementService(
            ILeaderValidationService leaderValidationService,
            IGroupMemberRepository memberRepository,
            IStudentGroupRepository groupRepository,
            IUserRepository userRepository,
            ITaskRepository taskRepository,
            IJiraIssueRepository jiraIssueRepository,
            IProjectRepository projectRepository,
            ICommitRepository commitRepository,
            ICommitStatisticRepository commitStatisticRepository,
            IRequirementRepository requirementRepository,
            IJiraIntegrationRepository jiraIntegrationRepository,
            IJiraApiService jiraApiService,
            IPersonalTaskStatisticRepository personalTaskStatisticRepository,
            IConfiguration configuration,
            ISrsDocumentRepository srsDocumentRepository,
            IAiChatService aiChatService)
        {
            _leaderValidationService = leaderValidationService;
            _memberRepository = memberRepository;
            _groupRepository = groupRepository;
            _userRepository = userRepository;
            _taskRepository = taskRepository;
            _jiraIssueRepository = jiraIssueRepository;
            _projectRepository = projectRepository;
            _commitRepository = commitRepository;
            _commitStatisticRepository = commitStatisticRepository;
            _requirementRepository = requirementRepository;
            _jiraIntegrationRepository = jiraIntegrationRepository;
            _jiraApiService = jiraApiService;
            _personalTaskStatisticRepository = personalTaskStatisticRepository;
            _srsDocumentRepository = srsDocumentRepository;
            _aiChatService = aiChatService;
            // Derive the same stable AES-GCM key as JiraIntegrationService
            var jwtKey = configuration["Jwt:Key"] ?? "JGMS_DEFAULT_ENCRYPTION_KEY_32CH";
            _encryptionKey = SHA256.HashData(Encoding.UTF8.GetBytes(jwtKey));
        }

        /// <summary>
        /// BR-055: Get all requirements for the leader's group project
        /// Validates that user is leader of the group
        /// </summary>
        public async Task<List<RequirementResponseDTO>> GetGroupRequirementsAsync(int userId, int groupId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null) throw new Exception("Group not found");

            await _leaderValidationService.ValidateLeaderAccessAsync(userId, groupId);

            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project == null) return new List<RequirementResponseDTO>();

            var requirements = await _requirementRepository.GetByProjectIdAsync(project.ProjectId);
            return requirements.Select(MapRequirementToDTO).ToList();
        }

        /// <summary>
        /// BR-055: Create a requirement for the leader's group.
        /// If Jira integration is configured, creates the issue in Jira first,
        /// then stores the requirement locally linked to the synced Jira issue.
        /// </summary>
        public async Task<RequirementResponseDTO> CreateRequirementAsync(int userId, int groupId, CreateRequirementDTO dto)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null) throw new Exception("Group not found");

            await _leaderValidationService.ValidateLeaderAccessAsync(userId, groupId);

            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project == null) throw new Exception("No project found for this group");

            // Validate requirement code uniqueness
            if (await _requirementRepository.ExistsByCodeAsync(project.ProjectId, dto.RequirementCode))
                throw new Exception($"Requirement code '{dto.RequirementCode}' already exists in this project.");

            int? jiraIssueId = dto.JiraIssueId;
            string? jiraIssueKey = null;
            string? jiraStatus = null;

            // If a JiraIssueId is provided directly, just link to it
            if (jiraIssueId.HasValue)
            {
                var existingIssue = await _jiraIssueRepository.GetByIdAsync(jiraIssueId.Value);
                if (existingIssue == null)
                    throw new Exception($"Jira issue with id {jiraIssueId} not found. Sync issues first.");
                if (existingIssue.ProjectId != project.ProjectId)
                    throw new Exception("The specified Jira issue does not belong to this group's project.");

                // BR-026: Jira integration must be configured and synced first
                var integration = await _jiraIntegrationRepository.GetByProjectIdAsync(project.ProjectId);
                if (integration == null || integration.SyncStatus != SyncStatus.success)
                {
                    throw new Exception("Jira integration must be configured and synced first");
                }

                jiraIssueKey = existingIssue.IssueKey;
                jiraStatus = existingIssue.Status;

                // BR-030: One Jira Issue Per Requirement
                var linkedReq = await _requirementRepository.GetByJiraIssueIdAsync(jiraIssueId.Value);
                if (linkedReq != null)
                {
                    throw new Exception("This Jira issue is already mapped to another requirement");
                }
            }
            else
            {
                // Try to create the issue in Jira if integration is configured
                var integration = await _jiraIntegrationRepository.GetByProjectIdAsync(project.ProjectId);
                if (integration != null)
                {
                    var createdJiraIssue = await _jiraApiService.CreateIssueAsync(
                        integration.JiraUrl, integration.JiraEmail, TeamLeaderHelper.DecryptToken(integration.ApiToken, _encryptionKey),
                        new CreateJiraIssueDTO
                        {
                            ProjectKey = integration.ProjectKey,
                            Summary = dto.Title,
                            Description = dto.Description,
                            IssueType = dto.IssueType,
                            Priority = TeamLeaderHelper.ToJiraPriority(dto.Priority)
                        });

                    // Store the new issue locally
                    var newJiraIssue = new JiraIssue
                    {
                        ProjectId = project.ProjectId,
                        IssueKey = createdJiraIssue.IssueKey,
                        JiraId = createdJiraIssue.JiraId,
                        IssueType = createdJiraIssue.IssueType,
                        Summary = createdJiraIssue.Summary,
                        Description = createdJiraIssue.Description,
                        Priority = ParseJiraPriority(createdJiraIssue.Priority),
                        Status = createdJiraIssue.Status,
                        CreatedDate = createdJiraIssue.CreatedDate,
                        UpdatedDate = createdJiraIssue.UpdatedDate
                    };
                    await _jiraIssueRepository.AddAsync(newJiraIssue);
                    jiraIssueId = newJiraIssue.JiraIssueId;
                    jiraIssueKey = newJiraIssue.IssueKey;
                    jiraStatus = newJiraIssue.Status;
                }
            }

            var requirement = new Requirement
            {
                ProjectId = project.ProjectId,
                RequirementCode = dto.RequirementCode,
                Title = dto.Title,
                Description = dto.Description,
                JiraIssueId = jiraIssueId,
                RequirementType = ParseRequirementType(dto.RequirementType),
                Priority = TeamLeaderHelper.ParsePriorityLevel(dto.Priority),
                CreatedBy = userId
            };

            await _requirementRepository.AddAsync(requirement);

            // Reload with navigation properties
            var saved = await _requirementRepository.GetByIdAsync(requirement.RequirementId);
            var result = MapRequirementToDTO(saved!);
            result.JiraIssueKey = jiraIssueKey;
            result.JiraStatus = jiraStatus;
            result.IssueType = dto.IssueType;
            result.Priority = TeamLeaderHelper.ToJiraPriority(dto.Priority);
            return result;
        }

        /// <summary>
        /// BR-055: Update a requirement for the leader's group.
        /// Syncs updated fields back to Jira if integration is configured.
        /// </summary>
        public async Task<RequirementResponseDTO> UpdateRequirementAsync(int userId, int groupId, int requirementId, UpdateRequirementDTO dto)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null) throw new Exception("Group not found");

            await _leaderValidationService.ValidateLeaderAccessAsync(userId, groupId);

            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project == null) throw new Exception("No project found for this group");

            var requirement = await _requirementRepository.GetByIdAsync(requirementId);
            if (requirement == null) throw new Exception("Requirement not found");
            if (requirement.ProjectId != project.ProjectId)
                throw new Exception("Access denied. This requirement does not belong to your group's project.");

            if (dto.Title != null) requirement.Title = dto.Title;
            if (dto.Description != null) requirement.Description = dto.Description;
            if (dto.JiraIssueId.HasValue)
            {
                // BR-030: One Jira Issue Per Requirement
                var linkedReq = await _requirementRepository.GetByJiraIssueIdAsync(dto.JiraIssueId.Value);
                if (linkedReq != null && linkedReq.RequirementId != requirementId)
                {
                    throw new Exception("This Jira issue is already mapped to another requirement");
                }
                requirement.JiraIssueId = dto.JiraIssueId;
            }
            if (dto.RequirementType != null) requirement.RequirementType = ParseRequirementType(dto.RequirementType);
            if (dto.Priority != null) requirement.Priority = TeamLeaderHelper.ParsePriorityLevel(dto.Priority);

            await _requirementRepository.UpdateAsync(requirement);

            // Sync changes back to Jira if linked
            if (requirement.JiraIssueId.HasValue)
            {
                var jiraIssue = await _jiraIssueRepository.GetByIdAsync(requirement.JiraIssueId.Value);
                if (jiraIssue != null)
                {
                    var integration = await _jiraIntegrationRepository.GetByProjectIdAsync(project.ProjectId);
                    if (integration != null)
                    {
                        try
                        {
                            await _jiraApiService.UpdateIssueAsync(
                                integration.JiraUrl, integration.JiraEmail, TeamLeaderHelper.DecryptToken(integration.ApiToken, _encryptionKey),
                                jiraIssue.IssueKey,
                                new UpdateJiraIssueDTO
                                {
                                    Summary = dto.Title,
                                    Description = dto.Description,
                                    Priority = TeamLeaderHelper.ToJiraPriority(dto.Priority)
                                });

                            // Update local Jira issue cache
                            if (dto.Title != null) jiraIssue.Summary = dto.Title;
                            if (dto.Description != null) jiraIssue.Description = dto.Description;
                            if (dto.Priority != null) jiraIssue.Priority = ParseJiraPriority(dto.Priority);
                            jiraIssue.UpdatedDate = DateTime.UtcNow;
                            await _jiraIssueRepository.UpdateAsync(jiraIssue);
                        }
                        catch
                        {
                            // Jira sync failed — local update still succeeds
                        }
                    }
                }
            }

            var saved = await _requirementRepository.GetByIdAsync(requirement.RequirementId);
            return MapRequirementToDTO(saved!);
        }

        /// <summary>
        /// BR-055: Delete a requirement for the leader's group.
        /// Removes the linked Jira issue from local cache (does NOT delete from Jira —
        /// Jira issues are owned by the Jira project and managed there).
        /// </summary>
        public async System.Threading.Tasks.Task DeleteRequirementAsync(int userId, int groupId, int requirementId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null) throw new Exception("Group not found");

            await _leaderValidationService.ValidateLeaderAccessAsync(userId, groupId);

            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project == null) throw new Exception("No project found for this group");

            var requirement = await _requirementRepository.GetByIdAsync(requirementId);
            if (requirement == null) throw new Exception("Requirement not found");
            if (requirement.ProjectId != project.ProjectId)
                throw new Exception("Access denied. This requirement does not belong to your group's project.");

            // Preserve SRS history integrity: this requirement is part of one or more generated snapshots.
            if (await _requirementRepository.HasSrsReferencesAsync(requirementId))
            {
                throw new Exception("Cannot delete requirement because it is referenced by generated SRS documents. Remove or regenerate those SRS links first.");
            }

            // Detach tasks first so requirement deletion does not violate FK constraints.
            await _requirementRepository.UnlinkTasksAsync(requirementId);

            var linkedJiraIssueId = requirement.JiraIssueId;

            await _requirementRepository.DeleteAsync(requirementId);

            // If linked to Jira, delete the issue in Jira too
            if (linkedJiraIssueId.HasValue)
            {
                var jiraIssue = await _jiraIssueRepository.GetByIdAsync(linkedJiraIssueId.Value);
                if (jiraIssue != null)
                {
                    var integration = await _jiraIntegrationRepository.GetByProjectIdAsync(project.ProjectId);
                    if (integration != null)
                    {
                        try
                        {
                            var client = new System.Net.Http.HttpClient();
                            var authValue = Convert.ToBase64String(
                                System.Text.Encoding.UTF8.GetBytes($"{integration.JiraEmail}:{TeamLeaderHelper.DecryptToken(integration.ApiToken, _encryptionKey)}"));
                            client.DefaultRequestHeaders.Authorization =
                                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);
                            await client.DeleteAsync(
                                $"{integration.JiraUrl.TrimEnd('/')}/rest/api/3/issue/{jiraIssue.IssueKey}");
                        }
                        catch
                        {
                            // Jira delete failed — continue with local delete
                        }
                    }
                }
            }
        }

        /// <summary>
        /// BR-055: Organise requirements by returning them sorted by the given order.
        /// The hierarchy (Epic → Story → Task) is expressed via IssueType in Jira.
        /// This endpoint reorders the local display list.
        /// </summary>
        public async Task<List<RequirementResponseDTO>> ReorderRequirementsAsync(int userId, int groupId, ReorderRequirementsDTO dto)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null) throw new Exception("Group not found");

            await _leaderValidationService.ValidateLeaderAccessAsync(userId, groupId);

            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project == null) throw new Exception("No project found for this group");

            var requirements = await _requirementRepository.GetByProjectIdAsync(project.ProjectId);

            // Build an ordered response based on the supplied order list
            var ordered = dto.Items
                .OrderBy(i => i.Order)
                .Select(item => requirements.FirstOrDefault(r => r.RequirementId == item.RequirementId))
                .Where(r => r != null)
                .Select(r => MapRequirementToDTO(r!))
                .ToList();

            return ordered;
        }

        /// <summary>
        /// BR-055: Bulk-import all synced Jira issues that don't already have a linked requirement.
        /// Skips issues that are already linked. Auto-generates requirement codes (REQ-P{projectId}-001, ...).
        /// </summary>
        public async Task<BulkImportFromJiraResultDTO> ImportRequirementsFromJiraAsync(int userId, int groupId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null) throw new Exception("Group not found");

            await _leaderValidationService.ValidateLeaderAccessAsync(userId, groupId);

            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project == null) throw new Exception("No project found for this group");

            var integration = await _jiraIntegrationRepository.GetByProjectIdAsync(project.ProjectId);
            // BR-026: Jira integration must be configured and synced before creating requirements from Jira
            if (integration == null || integration.SyncStatus != SyncStatus.success)
                throw new Exception("Jira integration must be configured and synced first");

            var allIssues = await _jiraIssueRepository.GetByProjectIdAsync(project.ProjectId);
            if (!allIssues.Any())
                throw new Exception("No Jira issues found. Run a sync first.");

            var existingRequirements = await _requirementRepository.GetByProjectIdAsync(project.ProjectId);
            var alreadyLinkedIssueIds = existingRequirements
                .Where(r => r.JiraIssueId.HasValue)
                .Select(r => r.JiraIssueId!.Value)
                .ToHashSet();

            // Build a starting code counter for this project (e.g. REQ-P5-007 -> next is REQ-P5-008)
            var projectCodePrefix = $"REQ-P{project.ProjectId}-";
            var existingCodes = existingRequirements
                .Select(r => r.RequirementCode)
                .Where(c => c.StartsWith(projectCodePrefix, StringComparison.OrdinalIgnoreCase)
                            && int.TryParse(c[projectCodePrefix.Length..], out _))
                .Select(c => int.Parse(c[projectCodePrefix.Length..]))
                .ToList();
            var nextCodeNum = existingCodes.Any() ? existingCodes.Max() + 1 : 1;

            var result = new BulkImportFromJiraResultDTO();

            foreach (var issue in allIssues)
            {
                if (alreadyLinkedIssueIds.Contains(issue.JiraIssueId))
                {
                    result.Skipped++;
                    continue;
                }

                try
                {
                    var code = $"{projectCodePrefix}{nextCodeNum:D3}";
                    // Make sure it's unique (edge case: someone manually created a matching code)
                    while (await _requirementRepository.ExistsByCodeAsync(project.ProjectId, code))
                    {
                        nextCodeNum++;
                        code = $"{projectCodePrefix}{nextCodeNum:D3}";
                    }

                    var requirement = new Requirement
                    {
                        ProjectId = project.ProjectId,
                        JiraIssueId = issue.JiraIssueId,
                        RequirementCode = code,
                        Title = issue.Summary,
                        Description = issue.Description,
                        RequirementType = DAL.Models.RequirementType.functional,
                        Priority = issue.Priority.HasValue
                            ? MapJiraPriorityToLevel(issue.Priority.Value)
                            : PriorityLevel.medium,
                        CreatedBy = userId
                    };

                    await _requirementRepository.AddAsync(requirement);

                    var saved = await _requirementRepository.GetByIdAsync(requirement.RequirementId);
                    result.Requirements.Add(MapRequirementToDTO(saved!));
                    result.Imported++;
                    nextCodeNum++;
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    result.Errors.Add($"Failed to import issue {issue.IssueKey}: {ex.Message}");
                }
            }

            return result;
        }

        private static PriorityLevel MapJiraPriorityToLevel(JiraPriority priority) =>
            priority switch
            {
                JiraPriority.highest or JiraPriority.high => PriorityLevel.high,
                JiraPriority.low or JiraPriority.lowest => PriorityLevel.low,
                _ => PriorityLevel.medium
            };

        private static RequirementResponseDTO MapRequirementToDTO(Requirement r) => new()
        {
            RequirementId = r.RequirementId,
            ProjectId = r.ProjectId,
            JiraIssueId = r.JiraIssueId,
            JiraIssueKey = r.JiraIssue?.IssueKey,
            RequirementCode = r.RequirementCode,
            Title = r.Title,
            Description = r.Description,
            RequirementType = r.RequirementType.ToString(),
            IssueType = r.JiraIssue?.IssueType,
            Priority = r.Priority.ToString(),
            JiraStatus = r.JiraIssue?.Status,
            CreatedBy = r.CreatedBy,
            CreatedByName = r.CreatedByNavigation?.FullName,
            CreatedAt = r.CreatedAt,
            UpdatedAt = r.UpdatedAt
        };

        private static JiraPriority? ParseJiraPriority(string? priority) =>
            priority?.ToLower() switch
            {
                "highest" => JiraPriority.highest,
                "high" => JiraPriority.high,
                "medium" => JiraPriority.medium,
                "low" => JiraPriority.low,
                "lowest" => JiraPriority.lowest,
                _ => null
            };

        private static RequirementType ParseRequirementType(string? value) =>
            value?.ToLower().Replace("-", "_") switch
            {
                "non_functional" or "non-functional" => DAL.Models.RequirementType.non_functional,
                _ => DAL.Models.RequirementType.functional
            };

    }
}
