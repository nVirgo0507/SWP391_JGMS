using BLL.DTOs.Admin;
using BLL.DTOs.Jira;
using BLL.Services.Interface;
using DAL.Models;
using DAL.Repositories.Interface;
using System.Threading.Tasks;

namespace BLL.Services
{
    /// <summary>
    /// Service for team leader-scoped operations with group access control
    /// BR-055: Team Leader Group-Scoped Access - Team leaders can only manage their own group's project
    /// Validation: Check user is leader of the group via GROUP_MEMBER.is_leader
    /// Error Message: "Access denied. You are not the leader of this group."
    /// </summary>
    public class TeamLeaderService : ITeamLeaderService
    {
        private readonly IGroupMemberRepository _memberRepository;
        private readonly IStudentGroupRepository _groupRepository;
        private readonly IUserRepository _userRepository;
        private readonly ITaskRepository _taskRepository;
        private readonly IJiraIssueRepository _jiraIssueRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IRequirementRepository _requirementRepository;
        private readonly IJiraIntegrationRepository _jiraIntegrationRepository;
        private readonly IJiraApiService _jiraApiService;

        public TeamLeaderService(
            IGroupMemberRepository memberRepository,
            IStudentGroupRepository groupRepository,
            IUserRepository userRepository,
            ITaskRepository taskRepository,
            IJiraIssueRepository jiraIssueRepository,
            IProjectRepository projectRepository,
            IRequirementRepository requirementRepository,
            IJiraIntegrationRepository jiraIntegrationRepository,
            IJiraApiService jiraApiService)
        {
            _memberRepository = memberRepository;
            _groupRepository = groupRepository;
            _userRepository = userRepository;
            _taskRepository = taskRepository;
            _jiraIssueRepository = jiraIssueRepository;
            _projectRepository = projectRepository;
            _requirementRepository = requirementRepository;
            _jiraIntegrationRepository = jiraIntegrationRepository;
            _jiraApiService = jiraApiService;
        }

        /// <summary>
        /// BR-055: Validates that user is the leader of the group
        /// Throws exception if not the leader
        /// </summary>
        private async System.Threading.Tasks.Task ValidateLeaderAccessAsync(int userId, int groupId)
        {
            var groupMember = await _memberRepository.GetByGroupAndUserIdAsync(groupId, userId);
            
            if (groupMember == null || !groupMember.IsLeader.GetValueOrDefault(false))
            {
                throw new Exception("Access denied. You are not the leader of this group.");
            }
        }

        /// <summary>
        /// BR-055: Get project details for the leader's group
        /// Validates that user is leader of the group
        /// </summary>
        public async Task<ProjectResponseDTO?> GetGroupProjectAsync(int userId, int groupId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
            {
                throw new Exception("Group not found");
            }

            // BR-055: Validate user is leader of the group
            await ValidateLeaderAccessAsync(userId, groupId);

            // Get project from repository by groupId
            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project == null) return null;

            return new ProjectResponseDTO
            {
                ProjectId = project.ProjectId,
                GroupId = project.GroupId,
                ProjectName = project.ProjectName,
                Description = project.Description,
                StartDate = project.StartDate,
                EndDate = project.EndDate,
                CreatedAt = project.CreatedAt,
                UpdatedAt = project.UpdatedAt
            };
        }

        /// <summary>
        /// BR-055: Get all requirements for the leader's group project
        /// Validates that user is leader of the group
        /// </summary>
        public async Task<List<RequirementResponseDTO>> GetGroupRequirementsAsync(int userId, int groupId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null) throw new Exception("Group not found");

            await ValidateLeaderAccessAsync(userId, groupId);

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

            await ValidateLeaderAccessAsync(userId, groupId);

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
                jiraIssueKey = existingIssue.IssueKey;
                jiraStatus = existingIssue.Status;
            }
            else
            {
                // Try to create the issue in Jira if integration is configured
                var integration = await _jiraIntegrationRepository.GetByProjectIdAsync(project.ProjectId);
                if (integration != null)
                {
                    var createdJiraIssue = await _jiraApiService.CreateIssueAsync(
                        integration.JiraUrl, integration.JiraEmail, integration.ApiToken,
                        new CreateJiraIssueDTO
                        {
                            ProjectKey = integration.ProjectKey,
                            Summary = dto.Title,
                            Description = dto.Description,
                            IssueType = dto.IssueType,
                            Priority = dto.Priority
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
                Priority = ParsePriorityLevel(dto.Priority),
                CreatedBy = userId
            };

            await _requirementRepository.AddAsync(requirement);

            // Reload with navigation properties
            var saved = await _requirementRepository.GetByIdAsync(requirement.RequirementId);
            var result = MapRequirementToDTO(saved!);
            result.JiraIssueKey = jiraIssueKey;
            result.JiraStatus = jiraStatus;
            result.IssueType = dto.IssueType;
            result.Priority = dto.Priority;
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

            await ValidateLeaderAccessAsync(userId, groupId);

            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project == null) throw new Exception("No project found for this group");

            var requirement = await _requirementRepository.GetByIdAsync(requirementId);
            if (requirement == null) throw new Exception("Requirement not found");
            if (requirement.ProjectId != project.ProjectId)
                throw new Exception("Access denied. This requirement does not belong to your group's project.");

            if (dto.Title != null) requirement.Title = dto.Title;
            if (dto.Description != null) requirement.Description = dto.Description;
            if (dto.JiraIssueId.HasValue) requirement.JiraIssueId = dto.JiraIssueId;
            if (dto.RequirementType != null) requirement.RequirementType = ParseRequirementType(dto.RequirementType);
            if (dto.Priority != null) requirement.Priority = ParsePriorityLevel(dto.Priority);

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
                                integration.JiraUrl, integration.JiraEmail, integration.ApiToken,
                                jiraIssue.IssueKey,
                                new UpdateJiraIssueDTO
                                {
                                    Summary = dto.Title,
                                    Description = dto.Description,
                                    Priority = dto.Priority
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

            await ValidateLeaderAccessAsync(userId, groupId);

            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project == null) throw new Exception("No project found for this group");

            var requirement = await _requirementRepository.GetByIdAsync(requirementId);
            if (requirement == null) throw new Exception("Requirement not found");
            if (requirement.ProjectId != project.ProjectId)
                throw new Exception("Access denied. This requirement does not belong to your group's project.");

            // If linked to Jira, delete the issue in Jira too
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
                            var client = new System.Net.Http.HttpClient();
                            var authValue = Convert.ToBase64String(
                                System.Text.Encoding.UTF8.GetBytes($"{integration.JiraEmail}:{integration.ApiToken}"));
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

            await _requirementRepository.DeleteAsync(requirementId);
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

            await ValidateLeaderAccessAsync(userId, groupId);

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
        /// BR-055: Get all tasks for the leader's group
        /// Validates that user is leader of the group
        /// </summary>
        public async Task<List<TaskResponseDTO>> GetGroupTasksAsync(int userId, int groupId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null) throw new Exception("Group not found");

            await ValidateLeaderAccessAsync(userId, groupId);

            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project == null) return new List<TaskResponseDTO>();

            var tasks = await _taskRepository.GetTasksByProjectIdAsync(project.ProjectId);
            return tasks.Select(MapToResponseDTO).ToList();
        }

        /// <summary>
        /// BR-055: Create a task for the leader's group (optionally linked to a Jira issue by JiraIssueId)
        /// Validates that user is leader of the group
        /// </summary>
        public async Task<TaskResponseDTO> CreateTaskAsync(int userId, int groupId, CreateTaskDTO dto)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null) throw new Exception("Group not found");

            await ValidateLeaderAccessAsync(userId, groupId);

            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project == null) throw new Exception("No project found for this group");

            // If a JiraIssueId is provided, validate it belongs to this project
            if (dto.JiraIssueId.HasValue)
            {
                var jiraIssue = await _jiraIssueRepository.GetByIdAsync(dto.JiraIssueId.Value);
                if (jiraIssue == null)
                    throw new Exception($"Jira issue with id {dto.JiraIssueId} not found. Sync issues first.");
                if (jiraIssue.ProjectId != project.ProjectId)
                    throw new Exception("The specified Jira issue does not belong to this group's project.");

                // Prevent duplicate tasks for the same Jira issue
                var existing = await _taskRepository.GetByJiraIssueIdAsync(dto.JiraIssueId.Value);
                if (existing != null)
                    throw new Exception($"A task already exists for Jira issue id {dto.JiraIssueId} (TaskId: {existing.TaskId}).");
            }

            var task = new DAL.Models.Task
            {
                Title = dto.Title,
                Description = dto.Description,
                RequirementId = dto.RequirementId,
                JiraIssueId = dto.JiraIssueId,
                DueDate = dto.DueDate,
                Status = DAL.Models.TaskStatus.todo,
                Priority = PriorityLevel.medium
            };

            await _taskRepository.AddAsync(task);
            return MapToResponseDTO(task);
        }

        /// <summary>
        /// BR-055: Create a task pre-populated from a synced Jira issue key (e.g. "SWP391-5").
        /// Validates that user is leader and the issue belongs to the group's project.
        /// Prevents duplicate tasks for the same Jira issue.
        /// </summary>
        public async Task<TaskResponseDTO> CreateTaskFromJiraIssueAsync(int userId, int groupId, CreateTaskFromJiraIssueDTO dto)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null) throw new Exception("Group not found");

            await ValidateLeaderAccessAsync(userId, groupId);

            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project == null) throw new Exception("No project found for this group");

            // Look up the synced Jira issue by key
            var jiraIssue = await _jiraIssueRepository.GetByIssueKeyAsync(dto.IssueKey);
            if (jiraIssue == null)
                throw new Exception($"Jira issue '{dto.IssueKey}' not found. Run a sync first via POST /api/jira/projects/{project.ProjectId}/sync.");

            if (jiraIssue.ProjectId != project.ProjectId)
                throw new Exception($"Jira issue '{dto.IssueKey}' does not belong to this group's project.");

            // Prevent creating a duplicate task for the same Jira issue
            var existingTask = await _taskRepository.GetByJiraIssueIdAsync(jiraIssue.JiraIssueId);
            if (existingTask != null)
                throw new Exception($"A task already exists for '{dto.IssueKey}' (TaskId: {existingTask.TaskId}). Use the update endpoint to modify it.");

            // Validate assignee is a member of the group if provided
            if (dto.AssignedTo.HasValue && !await _memberRepository.IsMemberOfGroupAsync(groupId, dto.AssignedTo.Value))
                throw new Exception("The specified assignee is not a member of this group.");

            // Map Jira priority to internal enum
            PriorityLevel priority = PriorityLevel.medium;
            if (!string.IsNullOrEmpty(jiraIssue.Priority?.ToString()))
            {
            priority = jiraIssue.Priority switch
            {
                JiraPriority.highest => PriorityLevel.high,
                JiraPriority.high => PriorityLevel.high,
                JiraPriority.medium => PriorityLevel.medium,
                JiraPriority.low => PriorityLevel.low,
                JiraPriority.lowest => PriorityLevel.low,
                _ => PriorityLevel.medium
            };
            }

            var task = new DAL.Models.Task
            {
                Title = dto.TitleOverride ?? jiraIssue.Summary,
                Description = jiraIssue.Description,
                JiraIssueId = jiraIssue.JiraIssueId,
                AssignedTo = dto.AssignedTo,
                DueDate = dto.DueDate,
                Status = DAL.Models.TaskStatus.todo,
                Priority = priority
            };

            await _taskRepository.AddAsync(task);
            return MapToResponseDTO(task);
        }

        /// <summary>
        /// BR-055: Update a task for the leader's group
        /// Validates that user is leader of the group
        /// </summary>
        public async Task<TaskResponseDTO> UpdateTaskAsync(int userId, int groupId, int taskId, UpdateTaskDTO dto)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null) throw new Exception("Group not found");

            await ValidateLeaderAccessAsync(userId, groupId);

            var task = await _taskRepository.GetByIdAsync(taskId);
            if (task == null) throw new Exception("Task not found");

            if (dto.Title != null) task.Title = dto.Title;
            if (dto.Description != null) task.Description = dto.Description;
            if (dto.DueDate.HasValue) task.DueDate = dto.DueDate;
            if (dto.CompletedAt.HasValue) task.CompletedAt = dto.CompletedAt;

            await _taskRepository.UpdateAsync(task);
            return MapToResponseDTO(task);
        }

        /// <summary>
        /// BR-055: Assign task to team member
        /// Validates that user is leader of the group
        /// Verifies member is part of the group before assignment
        /// </summary>
        public async System.Threading.Tasks.Task AssignTaskAsync(int userId, int groupId, int taskId, int memberId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null) throw new Exception("Group not found");

            await ValidateLeaderAccessAsync(userId, groupId);

            if (!await _memberRepository.IsMemberOfGroupAsync(groupId, memberId))
                throw new Exception("Member is not part of this group");

            var task = await _taskRepository.GetByIdAsync(taskId);
            if (task == null) throw new Exception("Task not found");

            task.AssignedTo = memberId;
            await _taskRepository.UpdateAsync(task);
        }

        // ── Helper ──────────────────────────────────────────────────────────────

        private static TaskResponseDTO MapToResponseDTO(DAL.Models.Task task) => new()
        {
            TaskId = task.TaskId,
            RequirementId = task.RequirementId,
            JiraIssueId = task.JiraIssueId,
            AssignedTo = task.AssignedTo,
            AssignedToName = task.AssignedToNavigation?.FullName,
            Title = task.Title,
            Description = task.Description,
            DueDate = task.DueDate,
            CompletedAt = task.CompletedAt,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt
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

        private static PriorityLevel ParsePriorityLevel(string? value) =>
            value?.ToLower() switch
            {
                "high" or "highest" => PriorityLevel.high,
                "low" or "lowest" => PriorityLevel.low,
                _ => PriorityLevel.medium
            };

        /// <summary>
        /// BR-055: Get SRS document for the leader's group
        /// Validates that user is leader of the group
        /// </summary>
        public async Task<SrsDocumentResponseDTO?> GetGroupSrsDocumentAsync(int userId, int groupId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
            {
                throw new Exception("Group not found");
            }

            // BR-055: Validate user is leader of the group
            await ValidateLeaderAccessAsync(userId, groupId);

            // TODO: Get SRS document from repository
            return null;
        }

        /// <summary>
        /// BR-055: Create SRS document for the leader's group
        /// Validates that user is leader of the group
        /// </summary>
        public async Task<SrsDocumentResponseDTO> CreateSrsDocumentAsync(int userId, int groupId, CreateSrsDocumentDTO dto)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
            {
                throw new Exception("Group not found");
            }

            // BR-055: Validate user is leader of the group
            await ValidateLeaderAccessAsync(userId, groupId);

            // TODO: Create SRS document in repository
            throw new NotImplementedException("SRS repository needed");
        }

        /// <summary>
        /// BR-055: Update SRS document for the leader's group
        /// Validates that user is leader of the group
        /// </summary>
        public async Task<SrsDocumentResponseDTO> UpdateSrsDocumentAsync(int userId, int groupId, int srsId, UpdateSrsDocumentDTO dto)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
            {
                throw new Exception("Group not found");
            }

            // BR-055: Validate user is leader of the group
            await ValidateLeaderAccessAsync(userId, groupId);

            // TODO: Update SRS document in repository
            throw new NotImplementedException("SRS repository needed");
        }
    }
}
