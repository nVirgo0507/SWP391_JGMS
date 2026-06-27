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
    public class TeamLeaderTaskService : ITeamLeaderTaskService
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
        private readonly ITokenEncryptionService _tokenEncryption;
        private readonly ISrsDocumentRepository _srsDocumentRepository;
        private readonly IAiChatService _aiChatService;

        public TeamLeaderTaskService(
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
            IAiChatService aiChatService,
            ITokenEncryptionService tokenEncryption)
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
            _tokenEncryption = tokenEncryption;
        }

        /// <summary>
        /// BR-055: Get all tasks for the leader's group
        /// Validates that user is leader of the group
        /// </summary>
        public async Task<List<TaskResponseDTO>> GetGroupTasksAsync(int userId, int groupId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null) throw new Exception("Group not found");

            await _leaderValidationService.ValidateLeaderAccessAsync(userId, groupId);

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

            await _leaderValidationService.ValidateLeaderAccessAsync(userId, groupId);

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
                Priority = TeamLeaderHelper.ParsePriorityLevel(dto.Priority)
            };

            await _taskRepository.AddAsync(task);

            if (task.AssignedTo.HasValue)
            {
                await _personalTaskStatisticRepository.RecalculateForUserProjectAsync(task.AssignedTo.Value, project.ProjectId);
            }

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
                                GetJiraCredentials(integration).Url, GetJiraCredentials(integration).Email, GetJiraCredentials(integration).Token,
                                jiraIssue.IssueKey,
                                new UpdateJiraIssueDTO
                                {
                                    Summary = dto.Title,
                                    Description = dto.Description,
                                    Priority = TeamLeaderHelper.ToJiraPriority(dto.Priority),
                                    DueDate = dto.DueDate.HasValue ? dto.DueDate.Value.ToDateTime(TimeOnly.MinValue) : null
                                });

                            // Move to sprint if sprintId provided
                            if (dto.SprintId.HasValue && dto.SprintId.Value > 0)
                                await _jiraApiService.MoveIssueToSprintAsync(
                                    GetJiraCredentials(integration).Url, GetJiraCredentials(integration).Email, GetJiraCredentials(integration).Token,
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

            await _leaderValidationService.ValidateLeaderAccessAsync(userId, groupId);

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

            // Auto-link to the local requirement mapped to this Jira issue (if one exists)
            var linkedRequirement = await _requirementRepository.GetByJiraIssueIdAsync(jiraIssue.JiraIssueId);

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

            int? finalAssignee = dto.AssignedTo;
            if (!finalAssignee.HasValue && !string.IsNullOrEmpty(jiraIssue.AssigneeJiraId))
            {
                var mappedUser = await _userRepository.GetByJiraAccountIdAsync(jiraIssue.AssigneeJiraId);
                if (mappedUser != null)
                {
                    // Only auto-assign if they are part of the group
                    if (await _memberRepository.IsMemberOfGroupAsync(groupId, mappedUser.UserId))
                    {
                        finalAssignee = mappedUser.UserId;
                    }
                }
            }

            var task = new DAL.Models.Task
            {
                Title = dto.TitleOverride ?? jiraIssue.Summary,
                Description = jiraIssue.Description,
                RequirementId = linkedRequirement?.RequirementId,
                JiraIssueId = jiraIssue.JiraIssueId,
                AssignedTo = finalAssignee,
                DueDate = dto.DueDate,
                Status = DAL.Models.TaskStatus.todo,
                Priority = priority
            };

            await _taskRepository.AddAsync(task);

            if (task.AssignedTo.HasValue)
            {
                await _personalTaskStatisticRepository.RecalculateForUserProjectAsync(task.AssignedTo.Value, project.ProjectId);
            }

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
                        GetJiraCredentials(integration).Url, GetJiraCredentials(integration).Email, GetJiraCredentials(integration).Token,
                        jiraIssue.IssueKey,
                        new UpdateJiraIssueDTO
                        {
                            Summary = dto.TitleOverride, // null = no change
                            AssigneeAccountId = assigneeAccountId
                        });

                    // Move to sprint if sprintId provided and > 0
                    if (dto.SprintId.HasValue && dto.SprintId.Value > 0)
                        await _jiraApiService.MoveIssueToSprintAsync(
                            GetJiraCredentials(integration).Url, GetJiraCredentials(integration).Email, GetJiraCredentials(integration).Token,
                            jiraIssue.IssueKey, dto.SprintId.Value);

                    // Move to backlog if sprintId == 0 explicitly passed
                    // (null means "don't touch sprint", 0 means "move to backlog")
                    else if (dto.SprintId.HasValue && dto.SprintId.Value == 0)
                        await _jiraApiService.MoveIssueToBacklogAsync(
                            GetJiraCredentials(integration).Url, GetJiraCredentials(integration).Email, GetJiraCredentials(integration).Token,
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

            await _leaderValidationService.ValidateLeaderAccessAsync(userId, groupId);

            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project == null) throw new Exception("No project found for this group");

            var task = await _taskRepository.GetByIdAsync(taskId);
            if (task == null) throw new Exception("Task not found");
            var previousAssignee = task.AssignedTo;

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
            if (dto.Priority != null) task.Priority = TeamLeaderHelper.ParsePriorityLevel(dto.Priority);

            // Auto-set CompletedAt when status moves to done
            if (dto.Status?.ToLower() == "done" && task.CompletedAt == null)
                task.CompletedAt = DateTime.UtcNow;

            await _taskRepository.UpdateAsync(task);

            if (previousAssignee.HasValue)
            {
                await _personalTaskStatisticRepository.RecalculateForUserProjectAsync(previousAssignee.Value, project.ProjectId);
            }
            if (task.AssignedTo.HasValue && task.AssignedTo != previousAssignee)
            {
                await _personalTaskStatisticRepository.RecalculateForUserProjectAsync(task.AssignedTo.Value, project.ProjectId);
            }

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
                            string? assigneeJiraId = null;
                            if (dto.AssignedTo.HasValue)
                            {
                                var assigneeUser = await _userRepository.GetByIdAsync(dto.AssignedTo.Value);
                                if (assigneeUser != null && !string.IsNullOrEmpty(assigneeUser.JiraAccountId))
                                {
                                    assigneeJiraId = assigneeUser.JiraAccountId;
                                    jiraIssue.AssigneeJiraId = assigneeJiraId;
                                }
                            }

                            // Update fields
                            await _jiraApiService.UpdateIssueAsync(
                                GetJiraCredentials(integration).Url, GetJiraCredentials(integration).Email, GetJiraCredentials(integration).Token,
                                jiraIssue.IssueKey,
                                new UpdateJiraIssueDTO
                                {
                                    Summary = dto.Title,
                                    Description = dto.Description,
                                    Priority = TeamLeaderHelper.ToJiraPriority(dto.Priority),
                                    AssigneeAccountId = assigneeJiraId,
                                    DueDate = dto.DueDate.HasValue ? dto.DueDate.Value.ToDateTime(TimeOnly.MinValue) : null
                                });

                            // Transition status if changed
                            if (dto.Status != null)
                            {
                                var transitions = await _jiraApiService.GetAvailableTransitionsAsync(
                                    GetJiraCredentials(integration).Url, GetJiraCredentials(integration).Email, GetJiraCredentials(integration).Token,
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
                                        GetJiraCredentials(integration).Url, GetJiraCredentials(integration).Email, GetJiraCredentials(integration).Token,
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

            await _leaderValidationService.ValidateLeaderAccessAsync(userId, groupId);

            if (!await _memberRepository.IsMemberOfGroupAsync(groupId, memberId))
                throw new Exception("Member is not part of this group");

            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project == null) throw new Exception("No project found for this group");

            var task = await _taskRepository.GetByIdAsync(taskId);
            if (task == null) throw new Exception("Task not found");

            var previousAssignee = task.AssignedTo;

            task.AssignedTo = memberId;
            await _taskRepository.UpdateAsync(task);

            if (previousAssignee.HasValue)
            {
                await _personalTaskStatisticRepository.RecalculateForUserProjectAsync(previousAssignee.Value, project.ProjectId);
            }
            await _personalTaskStatisticRepository.RecalculateForUserProjectAsync(memberId, project.ProjectId);

            // Sync assignment back to Jira
            if (task.JiraIssueId.HasValue)
            {
                var jiraIssue = await _jiraIssueRepository.GetByIdAsync(task.JiraIssueId.Value);
                if (jiraIssue != null)
                {
                    var integration = await _jiraIntegrationRepository.GetByProjectIdAsync(project.ProjectId);
                    if (integration != null)
                    {
                        var memberUser = await _userRepository.GetByIdAsync(memberId);
                        if (memberUser != null && !string.IsNullOrEmpty(memberUser.JiraAccountId))
                        {
                            try
                            {
                                await _jiraApiService.UpdateIssueAsync(
                                    GetJiraCredentials(integration).Url, GetJiraCredentials(integration).Email, GetJiraCredentials(integration).Token,
                                    jiraIssue.IssueKey,
                                    new UpdateJiraIssueDTO
                                    {
                                        AssigneeAccountId = memberUser.JiraAccountId
                                    });

                                jiraIssue.AssigneeJiraId = memberUser.JiraAccountId;
                                await _jiraIssueRepository.UpdateAsync(jiraIssue);
                            }
                            catch { /* Jira sync failure is non-blocking */ }
                        }
                    }
                }
            }
        }

        public async System.Threading.Tasks.Task DeleteTaskAsync(int userId, int groupId, int taskId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null) throw new Exception("Group not found");

            await _leaderValidationService.ValidateLeaderAccessAsync(userId, groupId);

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
                                GetJiraCredentials(integration).Url, GetJiraCredentials(integration).Email, GetJiraCredentials(integration).Token,
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

            await _leaderValidationService.ValidateLeaderAccessAsync(userId, groupId);

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
                    GetJiraCredentials(integration).Url, GetJiraCredentials(integration).Email, GetJiraCredentials(integration).Token,
                    jiraIssue.IssueKey);
            else
                await _jiraApiService.MoveIssueToSprintAsync(
                    GetJiraCredentials(integration).Url, GetJiraCredentials(integration).Email, GetJiraCredentials(integration).Token,
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

            await _leaderValidationService.ValidateLeaderAccessAsync(userId, groupId);

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
                                GetJiraCredentials(integration).Url, GetJiraCredentials(integration).Email, GetJiraCredentials(integration).Token,
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

            await _leaderValidationService.ValidateLeaderAccessAsync(userId, groupId);

            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project == null) throw new Exception("No project found for this group");

            var integration = await _jiraIntegrationRepository.GetByProjectIdAsync(project.ProjectId);
            if (integration == null)
                throw new Exception("No Jira integration configured for this project. Ask an admin to set it up.");

            // ── Verify connection first ──────────────────────────────────────────
            var connected = await _jiraApiService.TestConnectionAsync(
                GetJiraCredentials(integration).Url, GetJiraCredentials(integration).Email, GetJiraCredentials(integration).Token);
            if (!connected)
                throw new Exception($"Cannot connect to Jira at '{integration.ActiveUrl}'. Check the API token and email in the integration config.");

            // ── Batch Load ───────────────────────────────────────────────────────
            var existingIssues = await _jiraIssueRepository.GetByProjectIdAsync(project.ProjectId);
            var issuesDict = existingIssues.ToDictionary(i => i.JiraIssueId);
            var issuesToUpdate = new HashSet<JiraIssue>();

            // ── Sync Tasks ───────────────────────────────────────────────────────
            var tasks = await _taskRepository.GetTasksByProjectIdAsync(project.ProjectId);
            foreach (var task in tasks.Where(t => t.JiraIssueId.HasValue))
            {
                if (!issuesDict.TryGetValue(task.JiraIssueId!.Value, out var jiraIssue))
                {
                    result.Warnings.Add($"Task {task.TaskId} '{task.Title}': JiraIssueId {task.JiraIssueId} not found locally — skipped.");
                    continue;
                }

                // Verify the issue still exists in Jira before trying to update
                try
                {
                    await _jiraApiService.GetIssueAsync(
                        GetJiraCredentials(integration).Url, GetJiraCredentials(integration).Email, GetJiraCredentials(integration).Token,
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
                    string? assigneeJiraId = null;
                    if (task.AssignedTo.HasValue)
                    {
                        var assigneeUser = await _userRepository.GetByIdAsync(task.AssignedTo.Value);
                        if (assigneeUser != null && !string.IsNullOrEmpty(assigneeUser.JiraAccountId))
                        {
                            assigneeJiraId = assigneeUser.JiraAccountId;
                        }
                    }

                    // Push field updates
                    await _jiraApiService.UpdateIssueAsync(
                        GetJiraCredentials(integration).Url, GetJiraCredentials(integration).Email, GetJiraCredentials(integration).Token,
                        jiraIssue.IssueKey,
                        new UpdateJiraIssueDTO
                        {
                            Summary = task.Title,
                            Description = task.Description,
                            Priority = TeamLeaderHelper.ToJiraPriority(task.Priority),
                            AssigneeAccountId = assigneeJiraId,
                            DueDate = task.DueDate.HasValue ? task.DueDate.Value.ToDateTime(TimeOnly.MinValue) : null
                        });

                    // Push status transition
                    var transitions = await _jiraApiService.GetAvailableTransitionsAsync(
                        GetJiraCredentials(integration).Url, GetJiraCredentials(integration).Email, GetJiraCredentials(integration).Token,
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
                            GetJiraCredentials(integration).Url, GetJiraCredentials(integration).Email, GetJiraCredentials(integration).Token,
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
                                    GetJiraCredentials(integration).Url, GetJiraCredentials(integration).Email, GetJiraCredentials(integration).Token,
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
                    issuesToUpdate.Add(jiraIssue);

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
                if (!issuesDict.TryGetValue(req.JiraIssueId!.Value, out var jiraIssue))
                {
                    result.Warnings.Add($"Requirement {req.RequirementId} '{req.RequirementCode}': JiraIssueId {req.JiraIssueId} not found locally — skipped.");
                    continue;
                }

                // Verify the issue still exists in Jira before trying to update
                try
                {
                    await _jiraApiService.GetIssueAsync(
                        GetJiraCredentials(integration).Url, GetJiraCredentials(integration).Email, GetJiraCredentials(integration).Token,
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
                        GetJiraCredentials(integration).Url, GetJiraCredentials(integration).Email, GetJiraCredentials(integration).Token,
                        jiraIssue.IssueKey,
                        new UpdateJiraIssueDTO
                        {
                            Summary = req.Title,
                            Description = req.Description,
                            Priority = TeamLeaderHelper.ToJiraPriority(req.Priority)
                        });

                    // Update local cache
                    jiraIssue.Summary = req.Title;
                    jiraIssue.Description = req.Description;
                    issuesToUpdate.Add(jiraIssue);

                    result.RequirementsSynced++;
                }
                catch (Exception ex)
                {
                    result.RequirementsFailed++;
                    result.Errors.Add($"Requirement {req.RequirementId} '{req.RequirementCode}' ({jiraIssue.IssueKey}): {ex.Message}");
                }
            }

            if (issuesToUpdate.Any())
            {
                await _jiraIssueRepository.UpdateRangeAsync(issuesToUpdate.ToList());
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
            Priority = TeamLeaderHelper.ToJiraPriority(task.Priority) ?? "Medium",
            DueDate = task.DueDate,
            WorkHours = task.WorkHours,
            CompletedAt = task.CompletedAt,
            SprintId = task.JiraIssue?.SprintId,
            SprintName = task.JiraIssue?.SprintName,
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


        public async Task<BulkImportTasksFromJiraResultDTO> ImportAssignedTasksFromJiraAsync(int userId, int groupId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null) throw new Exception("Group not found");

            await _leaderValidationService.ValidateLeaderAccessAsync(userId, groupId);

            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project == null) throw new Exception("No project found for this group");

            var integration = await _jiraIntegrationRepository.GetByProjectIdAsync(project.ProjectId);
            if (integration == null || integration.SyncStatus != SyncStatus.success)
                throw new Exception("Jira integration must be configured and synced first");

            var allIssues = await _jiraIssueRepository.GetByProjectIdAsync(project.ProjectId);
            if (!allIssues.Any())
                throw new Exception("No Jira issues found. Run a sync first.");

            var existingTasks = await _taskRepository.GetTasksByProjectIdAsync(project.ProjectId);
            var alreadyLinkedIssueIds = existingTasks
                .Where(t => t.JiraIssueId.HasValue)
                .Select(t => t.JiraIssueId!.Value)
                .ToHashSet();

            var allUsers = await _userRepository.GetAllAsync();
            var usersByJiraId = allUsers
                .Where(u => !string.IsNullOrEmpty(u.JiraAccountId))
                .GroupBy(u => u.JiraAccountId!)
                .ToDictionary(g => g.Key, g => g.First());

            var result = new BulkImportTasksFromJiraResultDTO();
            var usersToRecalculateStats = new HashSet<int>();

            foreach (var issue in allIssues)
            {
                if (alreadyLinkedIssueIds.Contains(issue.JiraIssueId))
                {
                    continue; // Skip silently
                }

                if (string.IsNullOrEmpty(issue.AssigneeJiraId))
                {
                    continue; // Skip if no assignee
                }

                if (!usersByJiraId.TryGetValue(issue.AssigneeJiraId, out var mappedUser))
                {
                    result.Skipped++;
                    continue; // User is not mapped
                }

                if (!await _memberRepository.IsMemberOfGroupAsync(groupId, mappedUser.UserId))
                {
                    result.Skipped++;
                    continue; // User is mapped but not in this group
                }

                try
                {
                    var linkedRequirement = await _requirementRepository.GetByJiraIssueIdAsync(issue.JiraIssueId);
                    
                    PriorityLevel priority = PriorityLevel.medium;
                    if (issue.Priority.HasValue)
                    {
                        priority = issue.Priority switch
                        {
                            JiraPriority.highest => PriorityLevel.high,
                            JiraPriority.high => PriorityLevel.high,
                            JiraPriority.medium => PriorityLevel.medium,
                            JiraPriority.low => PriorityLevel.low,
                            JiraPriority.lowest => PriorityLevel.low,
                            _ => PriorityLevel.medium
                        };
                    }

                    var newStatus = issue.Status?.ToLower() switch
                    {
                        "to do" or "todo" or "to_do" => DAL.Models.TaskStatus.todo,
                        "in progress" or "in_progress" => DAL.Models.TaskStatus.in_progress,
                        "done" or "completed" => DAL.Models.TaskStatus.done,
                        _ => DAL.Models.TaskStatus.todo
                    };

                    var task = new DAL.Models.Task
                    {
                        Title = issue.Summary,
                        Description = issue.Description,
                        RequirementId = linkedRequirement?.RequirementId,
                        JiraIssueId = issue.JiraIssueId,
                        AssignedTo = mappedUser.UserId,
                        Status = newStatus,
                        Priority = priority,
                        CompletedAt = newStatus == DAL.Models.TaskStatus.done ? DateTime.UtcNow : null
                    };

                    await _taskRepository.AddAsync(task);
                    usersToRecalculateStats.Add(mappedUser.UserId);
                    result.Imported++;
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    result.Errors.Add($"Failed to import '{issue.IssueKey}': {ex.Message}");
                }
            }

            foreach (var uId in usersToRecalculateStats)
            {
                await _personalTaskStatisticRepository.RecalculateForUserProjectAsync(uId, project.ProjectId);
            }

            return result;
        }

        private (string Url, string Email, string Token) GetJiraCredentials(DAL.Models.JiraIntegration integration)
        {
            string url = integration.ActiveUrl;
            string token = !string.IsNullOrEmpty(integration.AccessToken)
                ? _tokenEncryption.Decrypt(integration.AccessToken)
                : _tokenEncryption.Decrypt(integration.ApiToken);
            string email = !string.IsNullOrEmpty(integration.AccessToken)
                ? "oauth@placeholder.com"
                : integration.JiraEmail;
            return (url, email, token);
        }
    }
}
