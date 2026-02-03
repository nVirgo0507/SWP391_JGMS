using BLL.DTOs.Admin;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    /// <summary>
    /// Team Member Service Interface
    /// BR-056: Team Member Self-Scoped Access - Team members can only update their own assigned tasks
    /// Validation: Check TASK.assigned_to matches current user_id
    /// Error Message: "Access denied. This task is not assigned to you."
    /// BR-057: Team Member Read-Only Requirements - Team members can view requirements but cannot create/edit/delete
    /// Validation: Check user is part of the group before read access
    /// Error Message: "Only team leaders can manage requirements"
    /// </summary>
    public interface ITeamMemberService
    {
        #region Requirements Management (Read-Only)

        /// <summary>
        /// BR-057: View all requirements for the team member's group
        /// Team members can view requirements but cannot create/edit/delete
        /// Validates that user is part of the group before showing requirements
        /// </summary>
        Task<List<RequirementResponseDTO>> GetGroupRequirementsAsync(int userId, int groupId);

        #endregion

        #region Task Access

        /// <summary>
        /// BR-056: Get task details - Only if task is assigned to the user
        /// Validates that TASK.assigned_to matches user_id
        /// </summary>
        Task<TaskResponseDTO?> GetMyTaskAsync(int userId, int taskId);

        /// <summary>
        /// BR-056: Get all tasks assigned to the current team member
        /// Only returns tasks where assigned_to = user_id
        /// </summary>
        Task<List<TaskResponseDTO>> GetMyTasksAsync(int userId);

        #endregion

        #region Task Status Management

        /// <summary>
        /// BR-056: Update task status - Only for assigned tasks
        /// Validates that TASK.assigned_to matches user_id
        /// Allows updating task status and completion status
        /// </summary>
        Task<TaskResponseDTO> UpdateTaskStatusAsync(int userId, int taskId, UpdateTaskStatusDTO dto);

        /// <summary>
        /// BR-056: Mark task as completed
        /// Validates that TASK.assigned_to matches user_id
        /// Sets CompletedAt timestamp
        /// </summary>
        Task<TaskResponseDTO> CompleteTaskAsync(int userId, int taskId);

        #endregion

        #region Statistics & Reporting

        /// <summary>
        /// BR-056: Get personal task statistics
        /// Returns statistics for tasks assigned to the user
        /// </summary>
        Task<PersonalTaskStatisticResponseDTO?> GetMyTaskStatisticsAsync(int userId);

        /// <summary>
        /// BR-056: Get personal commit statistics
        /// Returns statistics for commits by the user
        /// </summary>
        Task<CommitStatisticResponseDTO?> GetMyCommitStatisticsAsync(int userId);

        #endregion
    }
}
