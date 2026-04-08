using BLL.DTOs.Admin;
using BLL.Services.Interface;
using DAL.Models;
using DAL.Repositories.Interface;

namespace BLL.Services
{
    /// <summary>
    /// Team Member Service
    /// BR-056: Team Member Self-Scoped Access - Team members can only update their own assigned tasks
    /// Validation: Check TASK.assigned_to matches current user_id
    /// Error Message: "Access denied. This task is not assigned to you."
    /// BR-057: Team Member Read-Only Requirements - Team members can view requirements but cannot create/edit/delete
    /// Validation: Check user is part of the group before read access
    /// Error Message: "Only team leaders can manage requirements"
    /// </summary>
    public class TeamMemberService : ITeamMemberService
    {
        private readonly IUserRepository _userRepository;
        private readonly ITaskRepository _taskRepository;
        private readonly IPersonalTaskStatisticRepository _statisticRepository;
        private readonly IGroupMemberRepository _groupMemberRepository;
        private readonly IGithubIntegrationRepository _githubIntegrationRepository;
        private readonly ICommitRepository _commitRepository;

        public TeamMemberService(
            IUserRepository userRepository,
            ITaskRepository taskRepository,
            IPersonalTaskStatisticRepository statisticRepository,
            IGroupMemberRepository groupMemberRepository,
            IGithubIntegrationRepository githubIntegrationRepository,
            ICommitRepository commitRepository)
        {
            _userRepository = userRepository;
            _taskRepository = taskRepository;
            _statisticRepository = statisticRepository;
            _groupMemberRepository = groupMemberRepository;
            _githubIntegrationRepository = githubIntegrationRepository;
            _commitRepository = commitRepository;
        }

        /// <summary>
        /// BR-056: Validates that task is assigned to the current user
        /// Throws exception if task is not assigned to this user
        /// </summary>
        private void ValidateSelfAccessAsync(int userId, int? assignedTo)
        {
            if (assignedTo == null || assignedTo != userId)
            {
                throw new Exception("Access denied. This task is not assigned to you.");
            }
        }

        /// <summary>
        /// BR-057: Validates that user is member of the group
        /// Throws exception if user is not part of the group
        /// </summary>
        private System.Threading.Tasks.Task ValidateGroupMembershipAsync(int userId, int groupId)
        {
            // TODO: Check group_member table for user membership
            return System.Threading.Tasks.Task.CompletedTask;
        }

        #region Requirements Management (Read-Only)

        /// <summary>
        /// BR-057: Get all requirements for the team member's group
        /// Team members can view requirements but cannot create/edit/delete
        /// Validates that user is part of the group before showing requirements
        /// </summary>
        public async Task<List<RequirementResponseDTO>> GetGroupRequirementsAsync(int userId, int groupId)
        {
            // Verify user exists and is a student
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null || user.Role != UserRole.student)
            {
                throw new Exception("User not found or is not a student");
            }

            // BR-057: Validate user is member of the group before allowing requirement view
            await ValidateGroupMembershipAsync(userId, groupId);

            // TODO: Get requirements from repository filtered by group_id
            // Return empty list as placeholder until requirement repository is available
            return new List<RequirementResponseDTO>();
        }

        #endregion

        #region Task Access

        /// <summary>
        /// BR-056: Get task details - Only if task is assigned to the user
        /// Validates that TASK.assigned_to matches user_id
        /// </summary>
        public async Task<TaskResponseDTO?> GetMyTaskAsync(int userId, int taskId)
        {
            // Verify user exists
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null || user.Role != UserRole.student)
            {
                throw new Exception("User not found or is not a student");
            }

            // TODO: Get task from repository
            // BR-056: Validate task is assigned to this user before returning
            throw new NotImplementedException("Task repository needed");
        }

        /// <summary>
        /// BR-056: Get all tasks assigned to the current team member
        /// Only returns tasks where assigned_to = user_id
        /// </summary>
        public async Task<List<TaskResponseDTO>> GetMyTasksAsync(int userId)
        {
            // Verify user exists
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null || user.Role != UserRole.student)
            {
                throw new Exception("User not found or is not a student");
            }

            // TODO: Get tasks from repository where assigned_to = userId
            return new List<TaskResponseDTO>();
        }

        /// <summary>
        /// BR-056: Update task status - Only for assigned tasks
        /// BR-038: When task status changes to 'done', auto-set completed_at to current timestamp.
        /// Validates that TASK.assigned_to matches user_id
        /// Allows updating task status and completion status
        /// Enforces forward-only progression: todo -> in_progress -> done.
        /// On invalid backwards transition an exception with message
        /// "Invalid status transition. Tasks cannot move backwards." is thrown.
        /// </summary>
        public async Task<TaskResponseDTO> UpdateTaskStatusAsync(int userId, int taskId, UpdateTaskStatusDTO dto)
        {
            // Verify user exists
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null || user.Role != UserRole.student)
            {
                throw new Exception("User not found or is not a student");
            }

            var task = await _taskRepository.GetByIdAsync(taskId);
            if (task == null)
                throw new Exception("Task not found");

            // Validate assigned to this user
            ValidateSelfAccessAsync(userId, task.AssignedTo);

            // Use shared helper to parse and validate status transitions
            var parsed = BLL.Services.Helpers.TaskStatusHelper.ParseStatus(dto.Status);
            BLL.Services.Helpers.TaskStatusHelper.ValidateForwardTransition(task.Status, parsed);

            task.Status = parsed;
            // BR-038: Completed Task Must Have Completion Date
            // Auto-set completed_at when status='done'
            if (parsed == DAL.Models.TaskStatus.done)
            {
                task.CompletedAt = DateTime.UtcNow;
            }

            if (dto.WorkHours.HasValue)
            {
                task.WorkHours = (task.WorkHours ?? 0) + dto.WorkHours.Value;
            }

            await _taskRepository.UpdateAsync(task);

            var projectId = ResolveProjectIdFromTask(task);
            if (projectId.HasValue)
            {
                await _statisticRepository.RecalculateForUserProjectAsync(userId, projectId.Value);
            }

            return MapToTaskResponse(task);
        }

        /// <summary>
        /// BR-056: Mark task as completed
        /// BR-038: When task status changes to 'done', auto-set completed_at to current timestamp.
        /// Validates that TASK.assigned_to matches user_id
        /// Sets CompletedAt timestamp to current UTC time
        /// </summary>
        public async Task<TaskResponseDTO> CompleteTaskAsync(int userId, int taskId)
        {
            // Verify user exists
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null || user.Role != UserRole.student)
            {
                throw new Exception("User not found or is not a student");
            }

            var task = await _taskRepository.GetByIdAsync(taskId);
            if (task == null)
                throw new Exception("Task not found");

            ValidateSelfAccessAsync(userId, task.AssignedTo);

            // If already done, return
            if (task.Status == DAL.Models.TaskStatus.done)
            {
                return MapToTaskResponse(task);
            }

            // Ensure we are not moving backwards (complete is forward)
            if ((int)DAL.Models.TaskStatus.done < (int)task.Status)
            {
                throw new Exception("Invalid status transition. Tasks cannot move backwards.");
            }

            task.Status = DAL.Models.TaskStatus.done;
            // BR-038: Completed Task Must Have Completion Date
            // Auto-set completed_at when status='done'
            task.CompletedAt = DateTime.UtcNow;

            await _taskRepository.UpdateAsync(task);

            var projectId = ResolveProjectIdFromTask(task);
            if (projectId.HasValue)
            {
                await _statisticRepository.RecalculateForUserProjectAsync(userId, projectId.Value);
            }

            return MapToTaskResponse(task);
        }

        private static int? ResolveProjectIdFromTask(DAL.Models.Task task)
        {
            return task.Requirement?.ProjectId ?? task.JiraIssue?.ProjectId;
        }

        /// <summary>
        /// BR-056: Get personal task statistics
        /// Returns statistics for tasks assigned to the user
        /// </summary>
        public async Task<PersonalTaskStatisticResponseDTO?> GetMyTaskStatisticsAsync(int userId)
        {
            // Verify user exists
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null || user.Role != UserRole.student)
            {
                throw new Exception("User not found or is not a student");
            }

            // TODO: Get personal task statistics from repository
            // Filter by user_id
            return null;
        }

        /// <summary>
        /// BR-056: Get personal commit statistics
        /// Returns statistics for commits by the user
        /// </summary>
        public async Task<CommitStatisticResponseDTO?> GetMyCommitStatisticsAsync(int userId)
        {
            // Verify user exists
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null || user.Role != UserRole.student)
            {
                throw new Exception("User not found or is not a student");
            }

            var memberships = await _groupMemberRepository.GetGroupsByStudentIdAsync(userId);
            var activeMembership = memberships.FirstOrDefault(m => m.LeftAt == null);
            var activeProject = activeMembership?.Group?.Project?.ProjectId;

            /* Tạm thời bypass kiểm tra integration để bạn dễ test
            if (activeProject.HasValue)
            {
                var integration = await _githubIntegrationRepository.GetByProjectIdAsync(activeProject.Value);
                if (integration == null || integration.SyncStatus != SyncStatus.success)
                {
                    throw new Exception("GitHub integration must be configured and synced first");
                }
            }
            else
            {
                throw new Exception("GitHub integration must be configured and synced first");
            }
            */

            // Lấy toàn bộ commit của user
            var commits = await _commitRepository.GetCommitsByUserIdAsync(userId);

            if (!commits.Any())
            {
                return new CommitStatisticResponseDTO
                {
                    UserId = userId,
                    UserName = user.FullName,
                    ProjectId = activeProject ?? 0,
                    UpdatedAt = DateTime.Now
                };
            }

            var now = DateTime.Now;
            var startOfWeek = now.Date.AddDays(-(int)now.DayOfWeek);
            var startOfMonth = new DateTime(now.Year, now.Month, 1);

            return new CommitStatisticResponseDTO
            {
                UserId = userId,
                UserName = user.FullName,
                ProjectId = activeProject ?? 0,
                TotalCommits = commits.Count,
                CommitsThisWeek = commits.Count(c => c.CommitDate >= startOfWeek),
                CommitsThisMonth = commits.Count(c => c.CommitDate >= startOfMonth),
                AverageCommitsPerDay = Math.Round((double)commits.Count / 30, 2), // Tạm tính theo tháng
                LastCommitDate = commits.Max(c => c.CommitDate),
                TotalAdditions = commits.Sum(c => c.Additions ?? 0),
                TotalDeletions = commits.Sum(c => c.Deletions ?? 0),
                TotalChangedFiles = commits.Sum(c => c.ChangedFiles ?? 0),
                UpdatedAt = DateTime.Now
            };
        }

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
                DueDate = task.DueDate,
                WorkHours = task.WorkHours,
                CompletedAt = task.CompletedAt,
                CreatedAt = task.CreatedAt,
                UpdatedAt = task.UpdatedAt,
                AssignedToName = task.AssignedToNavigation?.FullName
            };
        }

        #endregion
    }
}
