using BLL.DTOs.Admin;
using BLL.DTOs.Jira;
using BLL.Services.Interface;
using DAL.Models;
using DAL.Repositories.Interface;
using Microsoft.AspNetCore.DataProtection;
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
        private readonly IDataProtector _dataProtector;
        private readonly ISrsDocumentRepository _srsDocumentRepository;

        public TeamLeaderService(
            IGroupMemberRepository memberRepository,
            IStudentGroupRepository groupRepository,
            IUserRepository userRepository,
            ITaskRepository taskRepository,
            IJiraIssueRepository jiraIssueRepository,
            IProjectRepository projectRepository,
            IRequirementRepository requirementRepository,
            IJiraIntegrationRepository jiraIntegrationRepository,
            IJiraApiService jiraApiService,
            IDataProtectionProvider dataProtectionProvider,
            ISrsDocumentRepository srsDocumentRepository)
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
            _dataProtector = dataProtectionProvider.CreateProtector("JiraApiToken");
            _srsDocumentRepository = srsDocumentRepository;
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
                        integration.JiraUrl, integration.JiraEmail, DecryptToken(integration.ApiToken),
                        new CreateJiraIssueDTO
                        {
                            ProjectKey = integration.ProjectKey,
                            Summary = dto.Title,
                            Description = dto.Description,
                            IssueType = dto.IssueType,
                            Priority = ToJiraPriority(dto.Priority)
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
            result.Priority = ToJiraPriority(dto.Priority);
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
                                integration.JiraUrl, integration.JiraEmail, DecryptToken(integration.ApiToken),
                                jiraIssue.IssueKey,
                                new UpdateJiraIssueDTO
                                {
                                    Summary = dto.Title,
                                    Description = dto.Description,
                                    Priority = ToJiraPriority(dto.Priority)
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
                                System.Text.Encoding.UTF8.GetBytes($"{integration.JiraEmail}:{DecryptToken(integration.ApiToken)}"));
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

            // Validate assignee is a group member
            if (dto.AssignedTo.HasValue && !await _memberRepository.IsMemberOfGroupAsync(groupId, dto.AssignedTo.Value))
                throw new Exception("The specified assignee is not a member of this group.");

            // Validate RequirementId belongs to this project
            if (dto.RequirementId.HasValue)
            {
                var req = await _requirementRepository.GetByIdAsync(dto.RequirementId.Value);
                if (req == null || req.ProjectId != project.ProjectId)
                    throw new Exception("The specified requirement does not belong to this group's project.");
            }

            // If a JiraIssueId is provided, validate it belongs to this project
            if (dto.JiraIssueId.HasValue)
            {
                var jiraIssue = await _jiraIssueRepository.GetByIdAsync(dto.JiraIssueId.Value);
                if (jiraIssue == null)
                    throw new Exception($"Jira issue with id {dto.JiraIssueId} not found. Sync issues first.");
                if (jiraIssue.ProjectId != project.ProjectId)
                    throw new Exception("The specified Jira issue does not belong to this group's project.");

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
                AssignedTo = dto.AssignedTo,
                DueDate = dto.DueDate,
                Status = ParseTaskStatus(dto.Status),
                Priority = ParsePriorityLevel(dto.Priority)
            };

            await _taskRepository.AddAsync(task);

            // Sync to Jira if the task is linked to a Jira issue
            if (task.JiraIssueId.HasValue)
            {
                var jiraIssue = await _jiraIssueRepository.GetByIdAsync(task.JiraIssueId.Value);
                if (jiraIssue != null)
                {
                    var integration = await _jiraIntegrationRepository.GetByProjectIdAsync(project.ProjectId);
                    if (integration != null)
                    {
                        try
                        {
                            // Update Jira issue summary/description if changed
                            await _jiraApiService.UpdateIssueAsync(
                                integration.JiraUrl, integration.JiraEmail, DecryptToken(integration.ApiToken),
                                jiraIssue.IssueKey,
                                new UpdateJiraIssueDTO
                                {
                                    Summary = dto.Title,
                                    Description = dto.Description,
                                    Priority = ToJiraPriority(dto.Priority)
                                });

                            // Move to sprint if sprintId provided
                            if (dto.SprintId.HasValue && dto.SprintId.Value > 0)
                                await _jiraApiService.MoveIssueToSprintAsync(
                                    integration.JiraUrl, integration.JiraEmail, DecryptToken(integration.ApiToken),
                                    jiraIssue.IssueKey, dto.SprintId.Value);
                        }
                        catch { /* Jira sync failure is non-blocking */ }
                    }
                }
            }

            var saved = await _taskRepository.GetByIdAsync(task.TaskId);
            return MapToResponseDTO(saved!);
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

            // Look up the synced Jira issue — accept either the Jira key (e.g. "SWP391-5")
            // or the internal numeric JiraIssueId (e.g. "10" or 10)
            JiraIssue? jiraIssue;
            if (int.TryParse(dto.IssueKey, out var internalId))
                jiraIssue = await _jiraIssueRepository.GetByIdAsync(internalId);
            else
                jiraIssue = await _jiraIssueRepository.GetByIssueKeyAsync(dto.IssueKey);

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

            // Sync changes back to Jira
            var integration = await _jiraIntegrationRepository.GetByProjectIdAsync(project.ProjectId);
            if (integration != null)
            {
                try
                {
                    // Push title override and assignee to Jira
                    string? assigneeAccountId = null;
                    if (dto.AssignedTo.HasValue)
                    {
                        var assignee = await _userRepository.GetByIdAsync(dto.AssignedTo.Value);
                        if (!string.IsNullOrEmpty(assignee?.JiraAccountId))
                            assigneeAccountId = assignee.JiraAccountId;
                    }

                    await _jiraApiService.UpdateIssueAsync(
                        integration.JiraUrl, integration.JiraEmail, DecryptToken(integration.ApiToken),
                        jiraIssue.IssueKey,
                        new UpdateJiraIssueDTO
                        {
                            Summary = dto.TitleOverride, // null = no change
                            AssigneeAccountId = assigneeAccountId
                        });

                    // Move to sprint if sprintId provided and > 0
                    if (dto.SprintId.HasValue && dto.SprintId.Value > 0)
                        await _jiraApiService.MoveIssueToSprintAsync(
                            integration.JiraUrl, integration.JiraEmail, DecryptToken(integration.ApiToken),
                            jiraIssue.IssueKey, dto.SprintId.Value);

                    // Move to backlog if sprintId == 0 explicitly passed
                    // (null means "don't touch sprint", 0 means "move to backlog")
                    else if (dto.SprintId.HasValue && dto.SprintId.Value == 0)
                        await _jiraApiService.MoveIssueToBacklogAsync(
                            integration.JiraUrl, integration.JiraEmail, DecryptToken(integration.ApiToken),
                            jiraIssue.IssueKey);
                }
                catch { /* Jira sync failure is non-blocking */ }
            }

            var saved = await _taskRepository.GetByIdAsync(task.TaskId);
            return MapToResponseDTO(saved!);
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

            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project == null) throw new Exception("No project found for this group");

            var task = await _taskRepository.GetByIdAsync(taskId);
            if (task == null) throw new Exception("Task not found");

            // Validate new assignee is a group member
            if (dto.AssignedTo.HasValue && !await _memberRepository.IsMemberOfGroupAsync(groupId, dto.AssignedTo.Value))
                throw new Exception("The specified assignee is not a member of this group.");

            // Validate new requirement belongs to this project
            if (dto.RequirementId.HasValue)
            {
                var req = await _requirementRepository.GetByIdAsync(dto.RequirementId.Value);
                if (req == null || req.ProjectId != project.ProjectId)
                    throw new Exception("The specified requirement does not belong to this group's project.");
                task.RequirementId = dto.RequirementId;
            }

            if (dto.Title != null) task.Title = dto.Title;
            if (dto.Description != null) task.Description = dto.Description;
            if (dto.AssignedTo.HasValue) task.AssignedTo = dto.AssignedTo;
            if (dto.DueDate.HasValue) task.DueDate = dto.DueDate;
            if (dto.CompletedAt.HasValue) task.CompletedAt = dto.CompletedAt;
            if (dto.Status != null) task.Status = ParseTaskStatus(dto.Status);
            if (dto.Priority != null) task.Priority = ParsePriorityLevel(dto.Priority);

            // Auto-set CompletedAt when status moves to done
            if (dto.Status?.ToLower() == "done" && task.CompletedAt == null)
                task.CompletedAt = DateTime.UtcNow;

            await _taskRepository.UpdateAsync(task);

            // Sync changes back to Jira
            if (task.JiraIssueId.HasValue)
            {
                var jiraIssue = await _jiraIssueRepository.GetByIdAsync(task.JiraIssueId.Value);
                if (jiraIssue != null)
                {
                    var integration = await _jiraIntegrationRepository.GetByProjectIdAsync(project.ProjectId);
                    if (integration != null)
                    {
                        try
                        {
                            // Update fields
                            await _jiraApiService.UpdateIssueAsync(
                                integration.JiraUrl, integration.JiraEmail, DecryptToken(integration.ApiToken),
                                jiraIssue.IssueKey,
                                new UpdateJiraIssueDTO
                                {
                                    Summary = dto.Title,
                                    Description = dto.Description,
                                    Priority = ToJiraPriority(dto.Priority)
                                });

                            // Transition status if changed
                            if (dto.Status != null)
                            {
                                var transitions = await _jiraApiService.GetAvailableTransitionsAsync(
                                    integration.JiraUrl, integration.JiraEmail, DecryptToken(integration.ApiToken),
                                    jiraIssue.IssueKey);

                                var targetName = dto.Status.ToLower() switch
                                {
                                    "todo" or "to_do" => "To Do",
                                    "in_progress" => "In Progress",
                                    "done" => "Done",
                                    _ => dto.Status
                                };

                                var transition = transitions.FirstOrDefault(t =>
                                    t.To.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase));

                                if (transition != null)
                                    await _jiraApiService.TransitionIssueAsync(
                                        integration.JiraUrl, integration.JiraEmail, DecryptToken(integration.ApiToken),
                                        jiraIssue.IssueKey, transition.Id);

                                // Update local cache
                                jiraIssue.Status = targetName;
                                await _jiraIssueRepository.UpdateAsync(jiraIssue);
                            }
                        }
                        catch { /* Jira sync failure is non-blocking */ }
                    }
                }
            }

            var saved = await _taskRepository.GetByIdAsync(task.TaskId);
            return MapToResponseDTO(saved!);
        }

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

        public async System.Threading.Tasks.Task DeleteTaskAsync(int userId, int groupId, int taskId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null) throw new Exception("Group not found");

            await ValidateLeaderAccessAsync(userId, groupId);

            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project == null) throw new Exception("No project found for this group");

            var task = await _taskRepository.GetByIdAsync(taskId);
            if (task == null) throw new Exception("Task not found");

            // Delete linked Jira issue if configured
            if (task.JiraIssueId.HasValue)
            {
                var jiraIssue = await _jiraIssueRepository.GetByIdAsync(task.JiraIssueId.Value);
                if (jiraIssue != null)
                {
                    var integration = await _jiraIntegrationRepository.GetByProjectIdAsync(project.ProjectId);
                    if (integration != null)
                    {
                        try
                        {
                            await _jiraApiService.DeleteIssueAsync(
                                integration.JiraUrl, integration.JiraEmail, DecryptToken(integration.ApiToken),
                                jiraIssue.IssueKey);
                        }
                        catch { /* non-blocking */ }
                    }
                }
            }

            await _taskRepository.DeleteAsync(taskId);
        }

        public async Task<TaskResponseDTO> MoveTaskToSprintAsync(int userId, int groupId, int taskId, int sprintId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null) throw new Exception("Group not found");

            await ValidateLeaderAccessAsync(userId, groupId);

            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project == null) throw new Exception("No project found for this group");

            var task = await _taskRepository.GetByIdAsync(taskId);
            if (task == null) throw new Exception("Task not found");

            if (!task.JiraIssueId.HasValue)
                throw new Exception("This task is not linked to a Jira issue. Link it first to use sprint management.");

            var jiraIssue = await _jiraIssueRepository.GetByIdAsync(task.JiraIssueId.Value);
            if (jiraIssue == null) throw new Exception("Linked Jira issue not found locally. Run a sync.");

            var integration = await _jiraIntegrationRepository.GetByProjectIdAsync(project.ProjectId);
            if (integration == null)
                throw new Exception("No Jira integration configured for this project.");

            if (sprintId == 0)
                await _jiraApiService.MoveIssueToBacklogAsync(
                    integration.JiraUrl, integration.JiraEmail, DecryptToken(integration.ApiToken),
                    jiraIssue.IssueKey);
            else
                await _jiraApiService.MoveIssueToSprintAsync(
                    integration.JiraUrl, integration.JiraEmail, DecryptToken(integration.ApiToken),
                    jiraIssue.IssueKey, sprintId);

            var saved = await _taskRepository.GetByIdAsync(taskId);
            var result = MapToResponseDTO(saved!);
            result.SprintId = sprintId == 0 ? null : sprintId;
            return result;
        }

        public async Task<TaskResponseDTO> LinkTaskToRequirementAsync(int userId, int groupId, int taskId, int requirementId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null) throw new Exception("Group not found");

            await ValidateLeaderAccessAsync(userId, groupId);

            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project == null) throw new Exception("No project found for this group");

            var task = await _taskRepository.GetByIdAsync(taskId);
            if (task == null) throw new Exception("Task not found");

            var requirement = await _requirementRepository.GetByIdAsync(requirementId);
            if (requirement == null) throw new Exception("Requirement not found");
            if (requirement.ProjectId != project.ProjectId)
                throw new Exception("The requirement does not belong to this group's project.");

            task.RequirementId = requirementId;
            await _taskRepository.UpdateAsync(task);

            // Create Jira issue link if both sides have Jira issues
            if (task.JiraIssueId.HasValue && requirement.JiraIssueId.HasValue)
            {
                var taskJiraIssue = await _jiraIssueRepository.GetByIdAsync(task.JiraIssueId.Value);
                var reqJiraIssue = await _jiraIssueRepository.GetByIdAsync(requirement.JiraIssueId.Value);

                if (taskJiraIssue != null && reqJiraIssue != null)
                {
                    var integration = await _jiraIntegrationRepository.GetByProjectIdAsync(project.ProjectId);
                    if (integration != null)
                    {
                        try
                        {
                            await _jiraApiService.CreateIssueLinkAsync(
                                integration.JiraUrl, integration.JiraEmail, DecryptToken(integration.ApiToken),
                                taskJiraIssue.IssueKey, reqJiraIssue.IssueKey, "Relates");
                        }
                        catch { /* non-blocking */ }
                    }
                }
            }

            var saved = await _taskRepository.GetByIdAsync(taskId);
            return MapToResponseDTO(saved!);
        }

        /// <summary>
        /// Push all local task and requirement changes to Jira in bulk.
        /// Iterates every locally-linked item and syncs title, description,
        /// status, priority and assignee back to the Jira issue.
        /// </summary>
        public async Task<JiraPushSyncResultDTO> SyncToJiraAsync(int userId, int groupId)
        {
            var result = new JiraPushSyncResultDTO { SyncTime = DateTime.UtcNow };

            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null) throw new Exception("Group not found");

            await ValidateLeaderAccessAsync(userId, groupId);

            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project == null) throw new Exception("No project found for this group");

            var integration = await _jiraIntegrationRepository.GetByProjectIdAsync(project.ProjectId);
            if (integration == null)
                throw new Exception("No Jira integration configured for this project. Ask an admin to set it up.");

            // ── Verify connection first ──────────────────────────────────────────
            var connected = await _jiraApiService.TestConnectionAsync(
                integration.JiraUrl, integration.JiraEmail, DecryptToken(integration.ApiToken));
            if (!connected)
                throw new Exception($"Cannot connect to Jira at '{integration.JiraUrl}'. Check the API token and email in the integration config.");

            // ── Sync Tasks ───────────────────────────────────────────────────────
            var tasks = await _taskRepository.GetTasksByProjectIdAsync(project.ProjectId);
            foreach (var task in tasks.Where(t => t.JiraIssueId.HasValue))
            {
                var jiraIssue = await _jiraIssueRepository.GetByIdAsync(task.JiraIssueId!.Value);
                if (jiraIssue == null)
                {
                    result.Warnings.Add($"Task {task.TaskId} '{task.Title}': JiraIssueId {task.JiraIssueId} not found locally — skipped.");
                    continue;
                }

                // Verify the issue still exists in Jira before trying to update
                try
                {
                    await _jiraApiService.GetIssueAsync(
                        integration.JiraUrl, integration.JiraEmail, DecryptToken(integration.ApiToken),
                        jiraIssue.IssueKey);
                }
                catch
                {
                    result.Warnings.Add($"Task {task.TaskId} '{task.Title}': Jira issue '{jiraIssue.IssueKey}' not found or not accessible in project '{integration.ProjectKey}'. " +
                        $"The issue may have been deleted in Jira or belongs to a different project. Run a pull-sync first: POST /api/jira/projects/{project.ProjectId}/sync");
                    result.TasksFailed++;
                    continue;
                }

                try
                {
                    // Push field updates
                    await _jiraApiService.UpdateIssueAsync(
                        integration.JiraUrl, integration.JiraEmail, DecryptToken(integration.ApiToken),
                        jiraIssue.IssueKey,
                        new UpdateJiraIssueDTO
                        {
                            Summary = task.Title,
                            Description = task.Description,
                            Priority = ToJiraPriority(task.Priority)
                        });

                    // Push status transition
                    var transitions = await _jiraApiService.GetAvailableTransitionsAsync(
                        integration.JiraUrl, integration.JiraEmail, DecryptToken(integration.ApiToken),
                        jiraIssue.IssueKey);

                    var targetStatusName = task.Status switch
                    {
                        DAL.Models.TaskStatus.in_progress => "In Progress",
                        DAL.Models.TaskStatus.done => "Done",
                        _ => "To Do"
                    };

                    var transition = transitions.FirstOrDefault(t =>
                        t.To.Name.Equals(targetStatusName, StringComparison.OrdinalIgnoreCase));

                    if (transition != null)
                        await _jiraApiService.TransitionIssueAsync(
                            integration.JiraUrl, integration.JiraEmail, DecryptToken(integration.ApiToken),
                            jiraIssue.IssueKey, transition.Id);
                    else
                        result.Warnings.Add($"Task {task.TaskId} '{task.Title}': no Jira transition found for status '{targetStatusName}' — status not synced.");

                    // Push assignee if user has a Jira account ID
                    if (task.AssignedTo.HasValue)
                    {
                        var assignee = await _userRepository.GetByIdAsync(task.AssignedTo.Value);
                        if (!string.IsNullOrEmpty(assignee?.JiraAccountId))
                        {
                            try
                            {
                                await _jiraApiService.UpdateIssueAsync(
                                    integration.JiraUrl, integration.JiraEmail, DecryptToken(integration.ApiToken),
                                    jiraIssue.IssueKey,
                                    new UpdateJiraIssueDTO { AssigneeAccountId = assignee.JiraAccountId });
                            }
                            catch
                            {
                                result.Warnings.Add($"Task {task.TaskId}: jira_account_id '{assignee.JiraAccountId}' is invalid or not a member of the Jira project — assignee not synced.");
                            }
                        }
                        else
                            result.Warnings.Add($"Task {task.TaskId}: assignee userId {task.AssignedTo} has no jira_account_id — assignee not synced.");
                    }

                    // Update local Jira issue cache
                    jiraIssue.Summary = task.Title;
                    jiraIssue.Description = task.Description;
                    jiraIssue.Status = targetStatusName;
                    await _jiraIssueRepository.UpdateAsync(jiraIssue);

                    result.TasksSynced++;
                }
                catch (Exception ex)
                {
                    result.TasksFailed++;
                    result.Errors.Add($"Task {task.TaskId} '{task.Title}' ({jiraIssue.IssueKey}): {ex.Message}");
                }
            }

            // ── Sync Requirements ────────────────────────────────────────────────
            var requirements = await _requirementRepository.GetByProjectIdAsync(project.ProjectId);
            foreach (var req in requirements.Where(r => r.JiraIssueId.HasValue))
            {
                var jiraIssue = await _jiraIssueRepository.GetByIdAsync(req.JiraIssueId!.Value);
                if (jiraIssue == null)
                {
                    result.Warnings.Add($"Requirement {req.RequirementId} '{req.RequirementCode}': JiraIssueId {req.JiraIssueId} not found locally — skipped.");
                    continue;
                }

                // Verify the issue still exists in Jira before trying to update
                try
                {
                    await _jiraApiService.GetIssueAsync(
                        integration.JiraUrl, integration.JiraEmail, DecryptToken(integration.ApiToken),
                        jiraIssue.IssueKey);
                }
                catch
                {
                    result.Warnings.Add($"Requirement {req.RequirementId} '{req.RequirementCode}': Jira issue '{jiraIssue.IssueKey}' not found or not accessible in project '{integration.ProjectKey}'. " +
                        $"Run a pull-sync first: POST /api/jira/projects/{project.ProjectId}/sync");
                    result.RequirementsFailed++;
                    continue;
                }

                try
                {
                    await _jiraApiService.UpdateIssueAsync(
                        integration.JiraUrl, integration.JiraEmail, DecryptToken(integration.ApiToken),
                        jiraIssue.IssueKey,
                        new UpdateJiraIssueDTO
                        {
                            Summary = req.Title,
                            Description = req.Description,
                            Priority = ToJiraPriority(req.Priority)
                        });

                    // Update local cache
                    jiraIssue.Summary = req.Title;
                    jiraIssue.Description = req.Description;
                    await _jiraIssueRepository.UpdateAsync(jiraIssue);

                    result.RequirementsSynced++;
                }
                catch (Exception ex)
                {
                    result.RequirementsFailed++;
                    result.Errors.Add($"Requirement {req.RequirementId} '{req.RequirementCode}' ({jiraIssue.IssueKey}): {ex.Message}");
                }
            }

            return result;
        }

        // ── Helper ──────────────────────────────────────────────────────────────

        private static TaskResponseDTO MapToResponseDTO(DAL.Models.Task task) => new()
        {
            TaskId = task.TaskId,
            RequirementId = task.RequirementId,
            RequirementCode = task.Requirement?.RequirementCode,
            JiraIssueId = task.JiraIssueId,
            JiraIssueKey = task.JiraIssue?.IssueKey,
            AssignedTo = task.AssignedTo,
            AssignedToName = task.AssignedToNavigation?.FullName,
            Title = task.Title,
            Description = task.Description,
            Status = task.Status.ToString(),
            Priority = ToJiraPriority(task.Priority),
            DueDate = task.DueDate,
            CompletedAt = task.CompletedAt,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt
        };

        private static DAL.Models.TaskStatus ParseTaskStatus(string? value) =>
            value?.ToLower().Replace(" ", "_").Replace("-", "_") switch
            {
                "in_progress" or "inprogress" => DAL.Models.TaskStatus.in_progress,
                "done" or "completed" => DAL.Models.TaskStatus.done,
                _ => DAL.Models.TaskStatus.todo
            };

        private string DecryptToken(string encryptedToken)
        {
            try { return _dataProtector.Unprotect(encryptedToken); }
            catch { return encryptedToken; } // fallback if token was stored unencrypted
        }

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
        /// Converts internal priority (enum toString or user input) to Jira-expected title case.
        /// Jira requires "High", "Medium", "Low", "Highest", "Lowest" — not lowercase.
        /// </summary>
        private static string? ToJiraPriority(string? priority) =>
            priority?.ToLower() switch
            {
                "highest" => "Highest",
                "high" => "High",
                "medium" => "Medium",
                "low" => "Low",
                "lowest" => "Lowest",
                _ => null // null = don't send priority field to Jira
            };

        private static string? ToJiraPriority(PriorityLevel priority) =>
            priority switch
            {
                PriorityLevel.high => "High",
                PriorityLevel.medium => "Medium",
                PriorityLevel.low => "Low",
                _ => "Medium"
            };

        /// <summary>
        /// Get all SRS documents for the leader's group project
        /// </summary>
        public async Task<List<SrsDocumentResponseDTO>> GetGroupSrsDocumentsAsync(int userId, int groupId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null) throw new Exception("Group not found");

            await ValidateLeaderAccessAsync(userId, groupId);

            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project == null) return new List<SrsDocumentResponseDTO>();

            var documents = await _srsDocumentRepository.GetByProjectIdAsync(project.ProjectId);
            return documents.Select(MapSrsToDTO).ToList();
        }

        /// <summary>
        /// Get a single SRS document by ID with included requirements
        /// </summary>
        public async Task<SrsDocumentResponseDTO?> GetGroupSrsDocumentAsync(int userId, int groupId, int documentId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null) throw new Exception("Group not found");

            await ValidateLeaderAccessAsync(userId, groupId);

            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project == null) return null;

            var doc = await _srsDocumentRepository.GetByIdAsync(documentId);
            if (doc == null || doc.ProjectId != project.ProjectId) return null;

            return MapSrsToDTO(doc);
        }

        /// <summary>
        /// Generate an SRS document from existing requirements.
        /// If ImportFromJira is true, auto-creates requirements from synced Jira issues first.
        /// Snapshots requirement data into SRS_INCLUDED_REQUIREMENT rows for traceability.
        /// </summary>
        public async Task<SrsDocumentResponseDTO> GenerateSrsDocumentAsync(int userId, int groupId, CreateSrsDocumentDTO dto)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null) throw new Exception("Group not found");

            await ValidateLeaderAccessAsync(userId, groupId);

            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project == null) throw new Exception("No project found for this group");

            // Auto-import requirements from synced Jira issues
            if (dto.ImportFromJira)
            {
                var jiraIssues = await _jiraIssueRepository.GetByProjectIdAsync(project.ProjectId);
                int imported = 0;

                foreach (var issue in jiraIssues)
                {
                    // Skip if a requirement already exists for this Jira issue
                    var existing = await _requirementRepository.GetByJiraIssueIdAsync(issue.JiraIssueId);
                    if (existing != null) continue;

                    // Map Jira issue type to requirement type
                    var reqType = issue.IssueType?.ToLower() switch
                    {
                        "bug" or "improvement" or "enhancement" => RequirementType.non_functional,
                        _ => RequirementType.functional
                    };

                    // Map Jira priority to internal priority
                    var priority = issue.Priority switch
                    {
                        JiraPriority.highest or JiraPriority.high => PriorityLevel.high,
                        JiraPriority.low or JiraPriority.lowest => PriorityLevel.low,
                        _ => PriorityLevel.medium
                    };

                    var requirement = new Requirement
                    {
                        ProjectId = project.ProjectId,
                        RequirementCode = issue.IssueKey,
                        Title = issue.Summary,
                        Description = issue.Description,
                        JiraIssueId = issue.JiraIssueId,
                        RequirementType = reqType,
                        Priority = priority,
                        CreatedBy = userId
                    };

                    await _requirementRepository.AddAsync(requirement);
                    imported++;
                }
            }

            // Get requirements to include
            var allRequirements = await _requirementRepository.GetByProjectIdAsync(project.ProjectId);
            if (!allRequirements.Any())
                throw new Exception("No requirements found for this project. Sync Jira issues first (POST /api/jira/projects/{projectId}/sync) or create requirements manually.");

            List<Requirement> selectedRequirements;
            if (dto.RequirementIds != null && dto.RequirementIds.Any())
            {
                selectedRequirements = allRequirements
                    .Where(r => dto.RequirementIds.Contains(r.RequirementId))
                    .ToList();

                if (!selectedRequirements.Any())
                    throw new Exception("None of the specified requirement IDs belong to this project.");
            }
            else
            {
                selectedRequirements = allRequirements.ToList();
            }

            // Auto-generate introduction and scope if not provided
            var introduction = dto.Introduction ?? 
                $"This Software Requirements Specification (SRS) document describes the functional and non-functional requirements for the \"{project.ProjectName}\" project. " +
                $"It is intended to serve as a reference for the development team and stakeholders.";

            var scope = dto.Scope ??
                $"This document covers all requirements for \"{project.ProjectName}\". " +
                $"It includes {selectedRequirements.Count(r => r.RequirementType == RequirementType.functional)} functional requirement(s) " +
                $"and {selectedRequirements.Count(r => r.RequirementType == RequirementType.non_functional)} non-functional requirement(s).";

            // Create the SRS document header
            var srsDocument = new SrsDocument
            {
                ProjectId = project.ProjectId,
                Version = dto.Version,
                DocumentTitle = dto.DocumentTitle,
                Introduction = introduction,
                Scope = scope,
                Status = DocumentStatus.draft,
                GeneratedBy = userId,
                GeneratedAt = DateTime.UtcNow
            };

            await _srsDocumentRepository.AddAsync(srsDocument);

            // Separate functional and non-functional requirements
            var functional = selectedRequirements
                .Where(r => r.RequirementType == RequirementType.functional)
                .OrderBy(r => r.RequirementCode)
                .ToList();

            var nonFunctional = selectedRequirements
                .Where(r => r.RequirementType == RequirementType.non_functional)
                .OrderBy(r => r.RequirementCode)
                .ToList();

            // Snapshot each requirement into SRS_INCLUDED_REQUIREMENT
            int sectionCounter = 1;
            foreach (var req in functional)
            {
                srsDocument.SrsIncludedRequirements.Add(new SrsIncludedRequirement
                {
                    DocumentId = srsDocument.DocumentId,
                    RequirementId = req.RequirementId,
                    SectionNumber = $"3.1.{sectionCounter}",
                    SnapshotTitle = req.Title,
                    SnapshotDescription = req.Description
                });
                sectionCounter++;
            }

            sectionCounter = 1;
            foreach (var req in nonFunctional)
            {
                srsDocument.SrsIncludedRequirements.Add(new SrsIncludedRequirement
                {
                    DocumentId = srsDocument.DocumentId,
                    RequirementId = req.RequirementId,
                    SectionNumber = $"3.2.{sectionCounter}",
                    SnapshotTitle = req.Title,
                    SnapshotDescription = req.Description
                });
                sectionCounter++;
            }

            await _srsDocumentRepository.UpdateAsync(srsDocument);

            // Reload to get nav properties
            var saved = await _srsDocumentRepository.GetByIdAsync(srsDocument.DocumentId);
            return MapSrsToDTO(saved!);
        }

        /// <summary>
        /// Update SRS document metadata
        /// </summary>
        public async Task<SrsDocumentResponseDTO> UpdateSrsDocumentAsync(int userId, int groupId, int documentId, UpdateSrsDocumentDTO dto)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null) throw new Exception("Group not found");

            await ValidateLeaderAccessAsync(userId, groupId);

            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project == null) throw new Exception("No project found for this group");

            var doc = await _srsDocumentRepository.GetByIdAsync(documentId);
            if (doc == null) throw new Exception("SRS document not found");
            if (doc.ProjectId != project.ProjectId)
                throw new Exception("This SRS document does not belong to your group's project.");

            if (dto.DocumentTitle != null) doc.DocumentTitle = dto.DocumentTitle;
            if (dto.Version != null) doc.Version = dto.Version;
            if (dto.Introduction != null) doc.Introduction = dto.Introduction;
            if (dto.Scope != null) doc.Scope = dto.Scope;
            if (dto.Status != null)
            {
                doc.Status = dto.Status.ToLower() switch
                {
                    "published" => DocumentStatus.published,
                    _ => DocumentStatus.draft
                };
            }

            await _srsDocumentRepository.UpdateAsync(doc);

            var saved = await _srsDocumentRepository.GetByIdAsync(doc.DocumentId);
            return MapSrsToDTO(saved!);
        }

        /// <summary>
        /// Generate a downloadable HTML file of the SRS document
        /// </summary>
        public async Task<(byte[] content, string fileName)> DownloadSrsDocumentAsync(int userId, int groupId, int documentId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null) throw new Exception("Group not found");

            await ValidateLeaderAccessAsync(userId, groupId);

            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project == null) throw new Exception("No project found for this group");

            var doc = await _srsDocumentRepository.GetByIdAsync(documentId);
            if (doc == null) throw new Exception("SRS document not found");
            if (doc.ProjectId != project.ProjectId)
                throw new Exception("This SRS document does not belong to your group's project.");

            var html = GenerateSrsHtml(doc, project);
            var bytes = System.Text.Encoding.UTF8.GetBytes(html);
            var safeTitle = doc.DocumentTitle.Replace(" ", "_").Replace("/", "_");
            var fileName = $"{safeTitle}_v{doc.Version}.html";

            return (bytes, fileName);
        }

        // ── SRS Helpers ─────────────────────────────────────────────────────────

        private static SrsDocumentResponseDTO MapSrsToDTO(SrsDocument doc) => new()
        {
            DocumentId = doc.DocumentId,
            ProjectId = doc.ProjectId,
            ProjectName = doc.Project?.ProjectName ?? "",
            Version = doc.Version,
            DocumentTitle = doc.DocumentTitle,
            Introduction = doc.Introduction,
            Scope = doc.Scope,
            FilePath = doc.FilePath,
            Status = doc.Status.ToString(),
            GeneratedBy = doc.GeneratedBy,
            GeneratedByName = doc.GeneratedByNavigation?.FullName,
            GeneratedAt = doc.GeneratedAt,
            CreatedAt = doc.CreatedAt,
            UpdatedAt = doc.UpdatedAt,
            Requirements = doc.SrsIncludedRequirements
                .OrderBy(r => r.SectionNumber)
                .Select(r => new SrsIncludedRequirementDTO
                {
                    RequirementId = r.RequirementId,
                    SectionNumber = r.SectionNumber,
                    RequirementCode = r.Requirement?.RequirementCode,
                    Title = r.SnapshotTitle,
                    Description = r.SnapshotDescription,
                    RequirementType = r.Requirement?.RequirementType.ToString(),
                    Priority = r.Requirement?.Priority.ToString()
                }).ToList()
        };

        private static string GenerateSrsHtml(SrsDocument doc, Project project)
        {
            var functional = doc.SrsIncludedRequirements
                .Where(r => r.SectionNumber != null && r.SectionNumber.StartsWith("3.1"))
                .OrderBy(r => r.SectionNumber)
                .ToList();

            var nonFunctional = doc.SrsIncludedRequirements
                .Where(r => r.SectionNumber != null && r.SectionNumber.StartsWith("3.2"))
                .OrderBy(r => r.SectionNumber)
                .ToList();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"en\"><head><meta charset=\"UTF-8\">");
            sb.AppendLine($"<title>{Escape(doc.DocumentTitle)}</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("body { font-family: 'Segoe UI', Arial, sans-serif; max-width: 900px; margin: 40px auto; padding: 0 20px; color: #333; line-height: 1.6; }");
            sb.AppendLine("h1 { text-align: center; border-bottom: 3px solid #2c3e50; padding-bottom: 10px; }");
            sb.AppendLine("h2 { color: #2c3e50; border-bottom: 1px solid #bdc3c7; padding-bottom: 5px; margin-top: 30px; }");
            sb.AppendLine("h3 { color: #34495e; }");
            sb.AppendLine(".meta { text-align: center; color: #7f8c8d; margin-bottom: 30px; }");
            sb.AppendLine("table { width: 100%; border-collapse: collapse; margin: 15px 0; }");
            sb.AppendLine("th, td { border: 1px solid #bdc3c7; padding: 10px; text-align: left; }");
            sb.AppendLine("th { background-color: #2c3e50; color: white; }");
            sb.AppendLine("tr:nth-child(even) { background-color: #f2f2f2; }");
            sb.AppendLine(".req-section { margin: 10px 0 20px 0; padding: 10px 15px; background: #f9f9f9; border-left: 4px solid #3498db; }");
            sb.AppendLine(".priority-high { color: #e74c3c; font-weight: bold; }");
            sb.AppendLine(".priority-medium { color: #f39c12; font-weight: bold; }");
            sb.AppendLine(".priority-low { color: #27ae60; font-weight: bold; }");
            sb.AppendLine("</style></head><body>");

            // Title page
            sb.AppendLine($"<h1>{Escape(doc.DocumentTitle)}</h1>");
            sb.AppendLine($"<div class=\"meta\">");
            sb.AppendLine($"<p><strong>Project:</strong> {Escape(project.ProjectName)}</p>");
            sb.AppendLine($"<p><strong>Version:</strong> {Escape(doc.Version)} &nbsp;|&nbsp; <strong>Status:</strong> {doc.Status}</p>");
            sb.AppendLine($"<p><strong>Generated by:</strong> {Escape(doc.GeneratedByNavigation?.FullName ?? "N/A")} &nbsp;|&nbsp; <strong>Date:</strong> {doc.GeneratedAt?.ToString("yyyy-MM-dd HH:mm") ?? "N/A"}</p>");
            sb.AppendLine("</div>");

            // Table of Contents
            sb.AppendLine("<h2>Table of Contents</h2>");
            sb.AppendLine("<ol>");
            sb.AppendLine("<li>Introduction</li>");
            sb.AppendLine("<li>Scope</li>");
            sb.AppendLine("<li>Requirements");
            sb.AppendLine("<ol><li>Functional Requirements</li><li>Non-Functional Requirements</li></ol>");
            sb.AppendLine("</li>");
            sb.AppendLine("<li>Requirements Summary</li>");
            sb.AppendLine("</ol>");

            // 1. Introduction
            sb.AppendLine("<h2>1. Introduction</h2>");
            sb.AppendLine($"<p>{Escape(doc.Introduction ?? "N/A")}</p>");

            // 2. Scope
            sb.AppendLine("<h2>2. Scope</h2>");
            sb.AppendLine($"<p>{Escape(doc.Scope ?? "N/A")}</p>");

            // 3. Requirements
            sb.AppendLine("<h2>3. Requirements</h2>");

            // 3.1 Functional
            sb.AppendLine("<h3>3.1 Functional Requirements</h3>");
            if (functional.Any())
            {
                foreach (var req in functional)
                {
                    var priorityClass = GetPriorityClass(req.Requirement?.Priority);
                    sb.AppendLine($"<div class=\"req-section\">");
                    sb.AppendLine($"<h4>{Escape(req.SectionNumber ?? "")} — {Escape(req.SnapshotTitle ?? "Untitled")} ({Escape(req.Requirement?.RequirementCode ?? "")})</h4>");
                    sb.AppendLine($"<p><strong>Priority:</strong> <span class=\"{priorityClass}\">{req.Requirement?.Priority}</span></p>");
                    sb.AppendLine($"<p>{Escape(req.SnapshotDescription ?? "No description provided.")}</p>");
                    sb.AppendLine("</div>");
                }
            }
            else
            {
                sb.AppendLine("<p><em>No functional requirements included.</em></p>");
            }

            // 3.2 Non-Functional
            sb.AppendLine("<h3>3.2 Non-Functional Requirements</h3>");
            if (nonFunctional.Any())
            {
                foreach (var req in nonFunctional)
                {
                    var priorityClass = GetPriorityClass(req.Requirement?.Priority);
                    sb.AppendLine($"<div class=\"req-section\">");
                    sb.AppendLine($"<h4>{Escape(req.SectionNumber ?? "")} — {Escape(req.SnapshotTitle ?? "Untitled")} ({Escape(req.Requirement?.RequirementCode ?? "")})</h4>");
                    sb.AppendLine($"<p><strong>Priority:</strong> <span class=\"{priorityClass}\">{req.Requirement?.Priority}</span></p>");
                    sb.AppendLine($"<p>{Escape(req.SnapshotDescription ?? "No description provided.")}</p>");
                    sb.AppendLine("</div>");
                }
            }
            else
            {
                sb.AppendLine("<p><em>No non-functional requirements included.</em></p>");
            }

            // 4. Summary table
            sb.AppendLine("<h2>4. Requirements Summary</h2>");
            sb.AppendLine("<table><thead><tr><th>Section</th><th>Code</th><th>Title</th><th>Type</th><th>Priority</th></tr></thead><tbody>");
            foreach (var req in doc.SrsIncludedRequirements.OrderBy(r => r.SectionNumber))
            {
                sb.AppendLine($"<tr>");
                sb.AppendLine($"<td>{Escape(req.SectionNumber ?? "")}</td>");
                sb.AppendLine($"<td>{Escape(req.Requirement?.RequirementCode ?? "")}</td>");
                sb.AppendLine($"<td>{Escape(req.SnapshotTitle ?? "")}</td>");
                sb.AppendLine($"<td>{req.Requirement?.RequirementType}</td>");
                sb.AppendLine($"<td>{req.Requirement?.Priority}</td>");
                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</tbody></table>");

            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        private static string Escape(string? text) =>
            System.Net.WebUtility.HtmlEncode(text ?? "");

        private static string GetPriorityClass(PriorityLevel? priority) =>
            priority switch
            {
                PriorityLevel.high => "priority-high",
                PriorityLevel.medium => "priority-medium",
                PriorityLevel.low => "priority-low",
                _ => ""
            };
    }
}


