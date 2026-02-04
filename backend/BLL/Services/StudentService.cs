﻿using BLL.DTOs.Student;
using AdminDTOs = BLL.DTOs.Admin;
using BLL.Helpers;
using BLL.Services.Interface;
using DAL.Models;
using DAL.Repositories.Interface;

namespace BLL.Services
{
    public class StudentService : IStudentService
    {
        private readonly IUserRepository _userRepository;
        private readonly ITaskRepository _taskRepository;
        private readonly ICommitRepository _commitRepository;
        private readonly IPersonalTaskStatisticRepository _statisticRepository;
        private readonly ISrsDocumentRepository _srsRepository;
        private readonly IGroupMemberRepository _groupMemberRepository;

        public StudentService(
            IUserRepository userRepository,
            ITaskRepository taskRepository,
            ICommitRepository commitRepository,
            IPersonalTaskStatisticRepository statisticRepository,
            ISrsDocumentRepository srsRepository,
            IGroupMemberRepository groupMemberRepository)
        {
            _userRepository = userRepository;
            _taskRepository = taskRepository;
            _commitRepository = commitRepository;
            _statisticRepository = statisticRepository;
            _srsRepository = srsRepository;
            _groupMemberRepository = groupMemberRepository;
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

            // Parse and update task status
            // Normalize input: trim first, then lowercase, replace separators, map synonyms
            // Accepts: "todo", "To Do", "to_do", "in progress", "in-progress", "done", "Done", etc.
            var normalized = dto.Status
                .Trim()                   // Trim whitespace first
                .ToLower()                // Convert to lowercase
                .Replace(" ", "_")        // "to do" → "to_do"
                .Replace("-", "_");       // "in-progress" → "in_progress"

            // Map common variants to actual enum values
            // "to_do" → "todo", "completed" → "done"
            var statusString = normalized switch
            {
                "to_do" => "todo",
                "completed" => "done",
                _ => normalized
            };

            if (Enum.TryParse<DAL.Models.TaskStatus>(statusString, true, out var taskStatus))
            {
                task.Status = taskStatus;

                // Set completion timestamp when status is done
                if (taskStatus == DAL.Models.TaskStatus.done)
                {
                    task.CompletedAt = DateTime.UtcNow;
                }
                else if (task.CompletedAt.HasValue && taskStatus != DAL.Models.TaskStatus.done)
                {
                    // If moving back from completed status, clear the completed date
                    task.CompletedAt = null;
                }
            }
            else
            {
                throw new Exception($"Invalid status: '{dto.Status}'. Valid values: 'todo'/'to do', 'in_progress'/'in progress', 'done'/'completed' (case-insensitive)");
            }

            // TODO: Integrate with Jira API when implemented
            // - Update Jira issue status via API if task.JiraIssueId is present
            // - Post dto.Comment as Jira comment if provided
            // - Log dto.WorkHours in Jira if provided
            // Example: await _jiraService.UpdateIssueAsync(task.JiraIssueId, dto.Status, dto.Comment, dto.WorkHours);

            await _taskRepository.UpdateAsync(task);
        }

        #endregion

        #region View Personal Statistics

        public async Task<PersonalStatisticsDTO> GetPersonalStatisticsAsync(int userId, int? projectId = null)
        {
            var statisticsDto = new PersonalStatisticsDTO();

            // Get task statistics
            if (projectId.HasValue)
            {
                var taskStats = await _statisticRepository.GetByUserIdAndProjectIdAsync(userId, projectId.Value);
                if (taskStats != null)
                {
                    statisticsDto.TotalTasks = taskStats.TotalTasks ?? 0;
                    statisticsDto.CompletedTasks = taskStats.CompletedTasks ?? 0;
                    statisticsDto.InProgressTasks = taskStats.InProgressTasks ?? 0;
                    statisticsDto.OverdueTasks = taskStats.OverdueTasks ?? 0;
                    statisticsDto.CompletionRate = taskStats.CompletionRate ?? 0;
                    statisticsDto.LastCalculated = taskStats.LastCalculated;
                }
            }
            else
            {
                // Calculate aggregate statistics across all projects
                var allStats = await _statisticRepository.GetByUserIdAsync(userId);
                statisticsDto.TotalTasks = allStats.Sum(s => s.TotalTasks ?? 0);
                statisticsDto.CompletedTasks = allStats.Sum(s => s.CompletedTasks ?? 0);
                statisticsDto.InProgressTasks = allStats.Sum(s => s.InProgressTasks ?? 0);
                statisticsDto.OverdueTasks = allStats.Sum(s => s.OverdueTasks ?? 0);
                statisticsDto.CompletionRate = statisticsDto.TotalTasks > 0
                    ? (decimal)statisticsDto.CompletedTasks / statisticsDto.TotalTasks * 100
                    : 0;
                statisticsDto.LastCalculated = allStats.Any()
                    ? allStats.Max(s => s.LastCalculated)
                    : null;
            }

            // Get commit statistics
            var commits = projectId.HasValue
                ? await _commitRepository.GetCommitsByUserIdAndProjectIdAsync(userId, projectId.Value)
                : await _commitRepository.GetCommitsByUserIdAsync(userId);

            statisticsDto.TotalCommits = commits.Count;
            statisticsDto.TotalAdditions = commits.Sum(c => c.Additions ?? 0);
            statisticsDto.TotalDeletions = commits.Sum(c => c.Deletions ?? 0);
            statisticsDto.TotalChangedFiles = commits.Sum(c => c.ChangedFiles ?? 0);
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

        #endregion
    }
}
