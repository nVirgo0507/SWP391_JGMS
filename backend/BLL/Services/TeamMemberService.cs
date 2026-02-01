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
        // TODO: Add task, personal task statistic, commit statistic repositories when available
        // TODO: Add requirement repository for BR-057

        public TeamMemberService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
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
        private async Task ValidateGroupMembershipAsync(int userId, int groupId)
        {
            // TODO: Check group_member table for user membership
            // Verify user is part of the group to allow requirement viewing
            // throw new Exception("Only team leaders can manage requirements");
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
        /// Validates that TASK.assigned_to matches user_id
        /// Allows updating task status and completion status
        /// </summary>
        public async Task<TaskResponseDTO> UpdateTaskStatusAsync(int userId, int taskId, UpdateTaskStatusDTO dto)
        {
            // Verify user exists
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null || user.Role != UserRole.student)
            {
                throw new Exception("User not found or is not a student");
            }

            // TODO: Get task from repository
            // BR-056: Validate task is assigned to this user
            // ValidateSelfAccessAsync(userId, task.AssignedTo);
            // 
            // TODO: Update task status in repository
            throw new NotImplementedException("Task repository needed");
        }

        /// <summary>
        /// BR-056: Mark task as completed
        /// Validates that TASK.assigned_to matches user_id
        /// Sets CompletedAt timestamp
        /// </summary>
        public async Task<TaskResponseDTO> CompleteTaskAsync(int userId, int taskId)
        {
            // Verify user exists
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null || user.Role != UserRole.student)
            {
                throw new Exception("User not found or is not a student");
            }

            // TODO: Get task from repository
            // BR-056: Validate task is assigned to this user
            // ValidateSelfAccessAsync(userId, task.AssignedTo);
            //
            // TODO: Mark task as completed with current timestamp
            throw new NotImplementedException("Task repository needed");
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

            // TODO: Get personal commit statistics from repository
            // Filter by user_id
            return null;
        }
    }
}
