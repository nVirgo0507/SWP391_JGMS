﻿using BLL.DTOs.Student;
using AdminDTOs = BLL.DTOs.Admin;
using BLL.Helpers;
using BLL.Services.Interface;
using DAL.Models;
using DAL.Repositories.Interface;
using System.Text.RegularExpressions;

namespace BLL.Services
{
    public class StudentService : IStudentService
    {
        private readonly IUserRepository _userRepository;
        private readonly ITaskRepository _taskRepository;
        private readonly ICommitRepository _commitRepository;
        private readonly ICommitStatisticRepository _commitStatisticRepository;
        private readonly IPersonalTaskStatisticRepository _statisticRepository;
        private readonly ISrsDocumentRepository _srsRepository;
        private readonly IGroupMemberRepository _groupMemberRepository;
        private readonly IJiraIssueRepository _jiraIssueRepository;

        public StudentService(
            IUserRepository userRepository,
            ITaskRepository taskRepository,
            ICommitRepository commitRepository,
            ICommitStatisticRepository commitStatisticRepository,
            IPersonalTaskStatisticRepository statisticRepository,
            ISrsDocumentRepository srsRepository,
            IGroupMemberRepository groupMemberRepository,
            IJiraIssueRepository jiraIssueRepository)
        {
            _userRepository = userRepository;
            _taskRepository = taskRepository;
            _commitRepository = commitRepository;
            _commitStatisticRepository = commitStatisticRepository;
            _statisticRepository = statisticRepository;
            _srsRepository = srsRepository;
            _groupMemberRepository = groupMemberRepository;
            _jiraIssueRepository = jiraIssueRepository;
        }

        #region View Assigned Tasks

        public async System.Threading.Tasks.Task<List<TaskResponseDTO>> GetMyTasksAsync(int userId)
        {
            var tasks = await _taskRepository.GetTasksByUserIdAsync(userId);
            return tasks.Select(t => MapToTaskResponse(t)).ToList();
        }

        public async System.Threading.Tasks.Task<TaskResponseDTO?> GetTaskByIdAsync(int taskId, int userId)
        {
            var task = await _taskRepository.GetByIdAsync(taskId);

            if (task == null)
            {
                return null;
            }

            // Ensure the task is assigned and belongs to the requesting user
            // Explicitly reject access to unassigned tasks (AssignedTo is null)
            if (!task.AssignedTo.HasValue || task.AssignedTo.Value != userId)
            {
                throw new UnauthorizedAccessException("You do not have permission to view this task");
            }

            return MapToTaskResponse(task);
        }

        #endregion

        #region Update Task Status

        /// <summary>
        /// BR-035: Update the status of a task assigned to the student.
        /// BR-038: When task status changes to 'done', auto-set completed_at to current timestamp.
        /// Enforces business rule: statuses may only progress forward: todo -> in_progress -> done.
        /// On invalid backwards transition an exception with message "Invalid status transition. Tasks cannot move backwards." is thrown.
        /// </summary>
        public async System.Threading.Tasks.Task UpdateTaskStatusAsync(int taskId, int userId, UpdateTaskStatusDTO dto)
        {
            var task = await _taskRepository.GetByIdAsync(taskId);

            if (task == null)
            {
                throw new Exception("Task not found");
            }

            // Ensure the task is assigned and belongs to the requesting user
            // Explicitly reject access to unassigned tasks (AssignedTo is null)
            if (!task.AssignedTo.HasValue || task.AssignedTo.Value != userId)
            {
                throw new UnauthorizedAccessException("You do not have permission to update this task");
            }

            // Parse and validate status using shared helper that enforces BR-035 (forward-only progression)
            var parsedStatus = BLL.Services.Helpers.TaskStatusHelper.ParseStatus(dto.Status);
            BLL.Services.Helpers.TaskStatusHelper.ValidateForwardTransition(task.Status, parsedStatus);

            task.Status = parsedStatus;
            // BR-038: Completed Task Must Have Completion Date
            // Auto-set completed_at when status='done'
            if (parsedStatus == DAL.Models.TaskStatus.done)
            {
                task.CompletedAt = DateTime.UtcNow;
            }

            if (dto.WorkHours.HasValue)
            {
                task.WorkHours = (task.WorkHours ?? 0) + dto.WorkHours.Value;
            }

            // TODO: Integrate with Jira API when implemented
            // - Update Jira issue status via API if task.JiraIssueId is present
            // - Post dto.Comment as Jira comment if provided
            // - Log dto.WorkHours in Jira if provided
            // Example: await _jiraService.UpdateIssueAsync(task.JiraIssueId, dto.Status, dto.Comment, dto.WorkHours);

            await _taskRepository.UpdateAsync(task);

            var projectId = ResolveProjectIdFromTask(task);
            if (projectId.HasValue)
            {
                await _statisticRepository.RecalculateForUserProjectAsync(userId, projectId.Value);
            }
        }

        public async System.Threading.Tasks.Task<CommitLineSuggestionResponseDTO> GenerateCommitLineSuggestionAsync(
            int userId,
            CommitLineSuggestionRequestDTO dto)
        {
            if (dto.TaskId == null && string.IsNullOrWhiteSpace(dto.JiraIssueKey))
            {
                throw new Exception("Provide either TaskId or JiraIssueKey");
            }

            DAL.Models.Task? task = null;
            JiraIssue? jiraIssue = null;

            if (dto.TaskId.HasValue)
            {
                task = await _taskRepository.GetByIdAsync(dto.TaskId.Value);
                if (task == null)
                {
                    throw new Exception("Task not found");
                }
                jiraIssue = task.JiraIssue;
            }
            else
            {
                var normalizedIssueKey = dto.JiraIssueKey!.Trim().ToUpperInvariant();
                jiraIssue = await _jiraIssueRepository.GetByIssueKeyAsync(normalizedIssueKey)
                    ?? await _jiraIssueRepository.GetByIssueKeyAsync(dto.JiraIssueKey!.Trim());

                if (jiraIssue == null)
                {
                    throw new Exception("Jira issue not found");
                }

                task = await _taskRepository.GetByJiraIssueIdAsync(jiraIssue.JiraIssueId);
            }

            if (task != null)
            {
                if (!task.AssignedTo.HasValue || task.AssignedTo.Value != userId)
                {
                    throw new UnauthorizedAccessException("You do not have permission to view this task");
                }
            }
            else if (jiraIssue != null)
            {
                var currentUser = await _userRepository.GetByIdAsync(userId);
                var isLeaderInProject = await IsUserLeaderOfProjectAsync(userId, jiraIssue.ProjectId);
                var isAssignee = JiraAccountIdMatches(currentUser?.JiraAccountId, jiraIssue.AssigneeJiraId);

                if (currentUser == null || (!isLeaderInProject && !isAssignee))
                {
                    throw new UnauthorizedAccessException("You do not have permission to view this Jira issue");
                }
            }

            var issueKey = !string.IsNullOrWhiteSpace(jiraIssue?.IssueKey)
                ? jiraIssue!.IssueKey.Trim()
                : task != null
                    ? $"TASK-{task.TaskId}"
                    : "TASK";

            var title = !string.IsNullOrWhiteSpace(task?.Title)
                ? task!.Title
                : jiraIssue?.Summary ?? "update task";

            var type = NormalizeCommitType(dto.Type) ?? InferCommitType(task, jiraIssue, title);
            var scopeSegment = BuildScopeSegment(dto.Scope);
            var subject = BuildCommitSubject(title);
            var issuePrefix = dto.IncludeIssueKey ? $"[{issueKey}] " : string.Empty;

            var primaryLine = $"{type}{scopeSegment}: {issuePrefix}{subject}";
            var escapedPrimaryLine = EscapeForDoubleQuotedArg(primaryLine);
            var gitCommitCommand = $"git commit -m \"{escapedPrimaryLine}\"";
            var gitHubCommand = $"git add . && {gitCommitCommand} && git push origin HEAD";

            var taskIdSuffix = task != null ? $" {task.TaskId}" : string.Empty;
            var alternatives = new List<string>
            {
                $"{type}{scopeSegment}: {subject}",
                $"{type}: {issuePrefix}{subject}",
                $"chore{scopeSegment}: {issuePrefix}update task{taskIdSuffix}"
            };

            return new CommitLineSuggestionResponseDTO
            {
                TaskId = task?.TaskId,
                JiraIssueKey = jiraIssue?.IssueKey,
                CommitLine = primaryLine,
                GitCommitCommand = gitCommitCommand,
                GitHubCommand = gitHubCommand,
                Alternatives = alternatives
                    .Where(a => !string.Equals(a, primaryLine, StringComparison.Ordinal))
                    .Distinct(StringComparer.Ordinal)
                    .ToList()
            };
        }

        #endregion

        #region View Personal Statistics

        public async Task<PersonalStatisticsDTO> GetPersonalStatisticsAsync(int userId, int? projectId = null)
        {
            var statisticsDto = new PersonalStatisticsDTO();

            // Get task statistics from live TASK rows to avoid stale/empty snapshot results.
            var tasks = await _taskRepository.GetTasksByUserIdAsync(userId);
            if (projectId.HasValue)
            {
                tasks = tasks
                    .Where(t => (t.Requirement?.ProjectId == projectId.Value) || (t.JiraIssue?.ProjectId == projectId.Value))
                    .ToList();
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
            statisticsDto.TotalTasks = tasks.Count;
            statisticsDto.CompletedTasks = tasks.Count(t => t.Status == DAL.Models.TaskStatus.done || t.CompletedAt.HasValue);
            statisticsDto.InProgressTasks = tasks.Count(t => t.Status == DAL.Models.TaskStatus.in_progress);
            statisticsDto.OverdueTasks = tasks.Count(t =>
                t.DueDate.HasValue
                && t.DueDate.Value < today
                && t.Status != DAL.Models.TaskStatus.done
                && !t.CompletedAt.HasValue);
            statisticsDto.CompletionRate = statisticsDto.TotalTasks > 0
                ? (decimal)statisticsDto.CompletedTasks / statisticsDto.TotalTasks * 100
                : 0;
            statisticsDto.LastCalculated = DateTime.UtcNow;

            // Get commit statistics (commit_statistics is the primary source).
            var commits = projectId.HasValue
                ? await _commitRepository.GetCommitsByUserIdAndProjectIdAsync(userId, projectId.Value)
                : await _commitRepository.GetCommitsByUserIdAsync(userId);

            var recalcProjectIds = projectId.HasValue
                ? new List<int> { projectId.Value }
                : commits.Select(c => c.ProjectId).Distinct().ToList();

            foreach (var pid in recalcProjectIds)
            {
                try
                {
                    await _commitStatisticRepository.RecalculateProjectStatisticsAsync(pid);
                }
                catch
                {
                    // Best-effort refresh: serve statistics from current commit rows even if snapshot refresh fails.
                }
            }

            if (projectId.HasValue)
            {
                var stat = await _commitStatisticRepository.GetLatestByUserAndProjectIdAsync(userId, projectId.Value);
                statisticsDto.TotalCommits = stat?.TotalCommits ?? commits.Count;
                statisticsDto.TotalAdditions = stat?.TotalAdditions ?? commits.Sum(c => c.Additions ?? 0);
                statisticsDto.TotalDeletions = stat?.TotalDeletions ?? commits.Sum(c => c.Deletions ?? 0);
                statisticsDto.TotalChangedFiles = stat?.TotalChangedFiles ?? commits.Sum(c => c.ChangedFiles ?? 0);
            }
            else
            {
                var stats = await _commitStatisticRepository.GetLatestByUserIdAsync(userId);
                if (stats.Any())
                {
                    statisticsDto.TotalCommits = stats.Sum(s => s.TotalCommits ?? 0);
                    statisticsDto.TotalAdditions = stats.Sum(s => s.TotalAdditions ?? 0);
                    statisticsDto.TotalDeletions = stats.Sum(s => s.TotalDeletions ?? 0);
                    statisticsDto.TotalChangedFiles = stats.Sum(s => s.TotalChangedFiles ?? 0);
                }
                else
                {
                    statisticsDto.TotalCommits = commits.Count;
                    statisticsDto.TotalAdditions = commits.Sum(c => c.Additions ?? 0);
                    statisticsDto.TotalDeletions = commits.Sum(c => c.Deletions ?? 0);
                    statisticsDto.TotalChangedFiles = commits.Sum(c => c.ChangedFiles ?? 0);
                }
            }

            statisticsDto.LastCommitDate = commits.Any() ? commits.Max(c => c.CommitDate) : null;

            return statisticsDto;
        }

        public async System.Threading.Tasks.Task<List<CommitHistoryDTO>> GetCommitHistoryAsync(int userId, int? projectId = null)
        {
            var commits = projectId.HasValue
                ? await _commitRepository.GetCommitsByUserIdAndProjectIdAsync(userId, projectId.Value)
                : await _commitRepository.GetCommitsByUserIdAsync(userId);

            return commits.Select(c => new CommitHistoryDTO
            {
                CommitId = c.CommitId,
                CommitMessage = c.CommitMessage,
                Additions = c.Additions,
                Deletions = c.Deletions,
                ChangedFiles = c.ChangedFiles,
                CommitDate = c.CommitDate,
                ProjectId = c.ProjectId,
                ProjectName = c.Project?.ProjectName
            }).ToList();
        }

        #endregion

        #region Profile Management

        public async Task<AdminDTOs.UserResponseDTO> GetMyProfileAsync(int userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);

            if (user == null)
            {
                throw new Exception("User not found");
            }

            return MapToUserResponse(user);
        }

        public async Task<AdminDTOs.UserResponseDTO> UpdateMyProfileAsync(int userId, UpdateProfileDTO dto)
        {
            var user = await _userRepository.GetByIdAsync(userId);

            if (user == null)
            {
                throw new Exception("User not found");
            }

            // Students can only update specific fields
            if (!string.IsNullOrEmpty(dto.Phone))
            {
                var normalizedPhone = PhoneHelper.NormalizePhone(dto.Phone);

                if (!PhoneHelper.IsValidVietnamesePhone(normalizedPhone))
                {
                    throw new Exception("Invalid Vietnamese phone number format. Expected: 0XXXXXXXXX (10 digits)");
                }

                // Check phone uniqueness if being changed
                if (normalizedPhone != user.Phone && await _userRepository.PhoneExistsAsync(normalizedPhone))
                {
                    throw new Exception("Phone number already exists in the system");
                }

                user.Phone = normalizedPhone;
            }

            if (!string.IsNullOrEmpty(dto.GithubUsername))
            {
                // Check uniqueness if being changed
                if (dto.GithubUsername != user.GithubUsername &&
                    await _userRepository.GithubUsernameExistsAsync(dto.GithubUsername))
                {
                    throw new Exception("GitHub username already exists in the system");
                }
                user.GithubUsername = dto.GithubUsername;
            }

            if (!string.IsNullOrEmpty(dto.JiraAccountId))
            {
                // Check uniqueness if being changed
                if (dto.JiraAccountId != user.JiraAccountId &&
                    await _userRepository.JiraAccountIdExistsAsync(dto.JiraAccountId))
                {
                    throw new Exception("Jira account ID already exists in the system");
                }
                user.JiraAccountId = dto.JiraAccountId;
            }

            // Validate that required fields are not empty after update
            // For students, phone, githubUsername, and jiraAccountId are required
            if (user.Role == DAL.Models.UserRole.student)
            {
                if (string.IsNullOrWhiteSpace(user.Phone))
                {
                    throw new Exception("Phone number is required for students");
                }

                if (string.IsNullOrWhiteSpace(user.GithubUsername))
                {
                    throw new Exception("GitHub username is required for students");
                }

                if (string.IsNullOrWhiteSpace(user.JiraAccountId))
                {
                    throw new Exception("Jira account ID is required for students");
                }
            }

            // UpdatedAt is set by repository layer (UserRepository.UpdateAsync)
            try
            {
                await _userRepository.UpdateAsync(user);
            }
            catch (Exception ex)
            {
                // Handle database unique constraint violations (race condition protection)
                DatabaseExceptionHandler.HandleUniqueConstraintViolation(ex);
                throw; // Re-throw if not handled
            }

            return MapToUserResponse(user);
        }

        #endregion

        #region Group Membership

        /// <summary>
        /// Returns the group the student is currently an active member of (LeftAt is null).
        /// Returns null if the student is not in any group yet.
        /// </summary>
        public async Task<MyGroupDTO?> GetMyGroupAsync(int userId)
        {
            var memberships = await _groupMemberRepository.GetGroupsByStudentIdAsync(userId);

            // Only consider active memberships (not left)
            var activeMembership = memberships.FirstOrDefault(m => m.LeftAt == null);
            if (activeMembership == null)
                return null;

            var group = activeMembership.Group;

            return new MyGroupDTO
            {
                GroupId = group.GroupId,
                GroupCode = group.GroupCode,
                GroupName = group.GroupName,
                IsLeader = activeMembership.IsLeader ?? false,
                JoinedAt = activeMembership.JoinedAt,
                LecturerName = group.Lecturer?.FullName ?? string.Empty,
                ProjectId = group.Project?.ProjectId,
                ProjectName = group.Project?.ProjectName,
                Members = group.GroupMembers
                    .Where(m => m.LeftAt == null)
                    .Select(m => new MyGroupMemberDTO
                    {
                        UserId = m.UserId,
                        FullName = m.User?.FullName ?? string.Empty,
                        Email = m.User?.Email ?? string.Empty,
                        IsLeader = m.IsLeader ?? false,
                        JoinedAt = m.JoinedAt
                    })
                    .ToList()
            };
        }

        #endregion

        #region SRS Document

        public async System.Threading.Tasks.Task<List<SrsDocumentDTO>> GetSrsDocumentsByProjectAsync(int projectId, int userId)
        {
            // Verify user has access to this project (is a member of a group in this project)
            var userGroups = await _groupMemberRepository.GetGroupsByStudentIdAsync(userId);
            var hasAccess = userGroups.Any(gm => gm.Group?.Project?.ProjectId == projectId);

            if (!hasAccess)
            {
                throw new UnauthorizedAccessException("You do not have access to this project");
            }

            var documents = await _srsRepository.GetByProjectIdAsync(projectId);
            return documents.Select(MapToSrsDocumentDTO).ToList();
        }

        public async System.Threading.Tasks.Task<SrsDocumentDTO?> GetSrsDocumentByIdAsync(int documentId, int userId)
        {
            var document = await _srsRepository.GetByIdAsync(documentId);

            if (document == null)
            {
                return null;
            }

            // Verify user has access to the project this document belongs to
            var userGroups = await _groupMemberRepository.GetGroupsByStudentIdAsync(userId);
            var hasAccess = userGroups.Any(gm => gm.Group?.Project?.ProjectId == document.ProjectId);

            if (!hasAccess)
            {
                throw new UnauthorizedAccessException("You do not have access to this document");
            }

            return MapToSrsDocumentDTO(document);
        }

        #endregion

        #region Mapping Methods

        private TaskResponseDTO MapToTaskResponse(DAL.Models.Task task)
        {
            return new TaskResponseDTO
            {
                TaskId = task.TaskId,
                RequirementId = task.RequirementId,
                JiraIssueId = task.JiraIssueId,
                AssignedTo = task.AssignedTo,
                Title = task.Title,
                Description = task.Description,
                Status = task.Status.ToString(),
                Priority = task.Priority.ToString(),
                DueDate = task.DueDate,
                WorkHours = task.WorkHours,
                CompletedAt = task.CompletedAt,
                CreatedAt = task.CreatedAt,
                UpdatedAt = task.UpdatedAt,
                AssignedToName = task.AssignedToNavigation?.FullName,
                JiraIssueKey = task.JiraIssue?.IssueKey,
                JiraStatus = task.JiraIssue?.Status
            };
        }

        private AdminDTOs.UserResponseDTO MapToUserResponse(User user)
        {
            return new AdminDTOs.UserResponseDTO
            {
                UserId = user.UserId,
                Email = user.Email,
                FullName = user.FullName,
                Role = user.Role,
                StudentCode = user.StudentCode,
                GithubUsername = user.GithubUsername,
                JiraAccountId = user.JiraAccountId,
                Phone = user.Phone,
                Status = user.Status,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };
        }

        private SrsDocumentDTO MapToSrsDocumentDTO(SrsDocument document)
        {
            return new SrsDocumentDTO
            {
                DocumentId = document.DocumentId,
                ProjectId = document.ProjectId,
                Version = document.Version,
                DocumentTitle = document.DocumentTitle,
                Introduction = document.Introduction,
                Scope = document.Scope,
                FilePath = document.FilePath,
                GeneratedBy = document.GeneratedBy,
                GeneratedByName = document.GeneratedByNavigation?.FullName,
                GeneratedAt = document.GeneratedAt
            };
        }

        private static string InferCommitType(DAL.Models.Task? task, JiraIssue? jiraIssue, string title)
        {
            var issueType = (jiraIssue?.IssueType ?? task?.JiraIssue?.IssueType)?.Trim().ToLowerInvariant();
            var loweredTitle = title.Trim().ToLowerInvariant();

            if (issueType == "bug" || loweredTitle.StartsWith("fix") || loweredTitle.Contains("bug"))
            {
                return "fix";
            }

            if (issueType == "story" || issueType == "feature" || loweredTitle.StartsWith("add") || loweredTitle.StartsWith("implement"))
            {
                return "feat";
            }

            if (issueType == "test")
            {
                return "test";
            }

            if (issueType == "documentation" || loweredTitle.StartsWith("doc"))
            {
                return "docs";
            }

            return "chore";
        }

        private static string EscapeForDoubleQuotedArg(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private async Task<bool> IsUserLeaderOfProjectAsync(int userId, int projectId)
        {
            var memberships = await _groupMemberRepository.GetGroupsByStudentIdAsync(userId);
            return memberships.Any(m =>
                m.LeftAt == null &&
                (m.IsLeader ?? false) &&
                m.Group?.Project?.ProjectId == projectId);
        }

        private static bool JiraAccountIdMatches(string? userJiraAccountId, string? assigneeJiraId)
        {
            if (string.IsNullOrWhiteSpace(userJiraAccountId) || string.IsNullOrWhiteSpace(assigneeJiraId))
            {
                return false;
            }

            return string.Equals(
                userJiraAccountId.Trim(),
                assigneeJiraId.Trim(),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string? NormalizeCommitType(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return null;
            }

            var normalized = input.Trim().ToLowerInvariant();
            var allowed = new HashSet<string> { "feat", "fix", "docs", "refactor", "test", "chore" };

            return allowed.Contains(normalized) ? normalized : null;
        }

        private static string BuildScopeSegment(string? scope)
        {
            if (string.IsNullOrWhiteSpace(scope))
            {
                return string.Empty;
            }

            var cleaned = Regex.Replace(scope.Trim().ToLowerInvariant(), "[^a-z0-9-_]", "-");
            cleaned = Regex.Replace(cleaned, "-+", "-").Trim('-');

            return string.IsNullOrWhiteSpace(cleaned) ? string.Empty : $"({cleaned})";
        }

        private static string BuildCommitSubject(string title)
        {
            var compact = Regex.Replace(title, "\\s+", " ").Trim();
            if (string.IsNullOrWhiteSpace(compact))
            {
                return "update task";
            }

            compact = compact.TrimEnd('.', ';', ':');
            return compact.Length <= 72 ? compact : compact[..72].TrimEnd();
        }

        private static int? ResolveProjectIdFromTask(DAL.Models.Task task)
        {
            return task.Requirement?.ProjectId ?? task.JiraIssue?.ProjectId;
        }

        public async Task<TaskStatisticsByStatusDTO> GetTaskStatisticsByStatusAsync(int userId)
        {
            var todoCount = await _taskRepository.CountTasksByStatusAsync(userId, "todo");
            var inProgressCount = await _taskRepository.CountTasksByStatusAsync(userId, "in_progress");
            var doneCount = await _taskRepository.CountTasksByStatusAsync(userId, "done");

            return new TaskStatisticsByStatusDTO
            {
                TodoTasks = todoCount,
                InProgressTasks = inProgressCount,
                DoneTasks = doneCount,
                TotalTasks = todoCount + inProgressCount + doneCount
            };
        }

        #endregion
    }
}
