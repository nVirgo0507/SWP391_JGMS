using BLL.DTOs.Admin;
using BLL.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace SWP391_JGMS.Controllers
{
    /// <summary>
    /// Team Member API endpoints with self-scoped and read-only access control
    /// BR-056: Team Member Self-Scoped Access - Team members can only update their own assigned tasks
    /// Validation: Check TASK.assigned_to matches current user_id
    /// Error Message: "Access denied. This task is not assigned to you."
    /// BR-057: Team Member Read-Only Requirements - Team members can view requirements but cannot create/edit/delete
    /// Validation: Check user is part of the group before read access
    /// Error Message: "Only team leaders can manage requirements"
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class TeamMemberController : ControllerBase
    {
        private readonly ITeamMemberService _teamMemberService;

        public TeamMemberController(ITeamMemberService teamMemberService)
        {
            _teamMemberService = teamMemberService;
        }

        #region Requirements Management (Read-Only)

        /// <summary>
        /// BR-057: Get all requirements for the team member's group
        /// Team members can view requirements but cannot create/edit/delete
        /// Validates that user is part of the group before showing requirements
        /// </summary>
        /// <param name="userId">The ID of the current team member user</param>
        /// <param name="groupId">The ID of the group</param>
        /// <returns>List of requirements for the group</returns>
        [HttpGet("groups/{groupId}/requirements")]
        [ProducesResponseType(typeof(List<RequirementResponseDTO>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetGroupRequirements([FromQuery] int userId, int groupId)
        {
            try
            {
                var requirements = await _teamMemberService.GetGroupRequirementsAsync(userId, groupId);
                return Ok(requirements);
            }
            catch (Exception ex)
            {
                // BR-057: Access denied if not member of the group
                if (ex.Message.Contains("Access denied"))
                    return Forbid();
                return BadRequest(new { message = ex.Message });
            }
        }

        #endregion

        #region Task Management

        /// <summary>
        /// BR-056: Get all tasks assigned to the current team member
        /// Only returns tasks where assigned_to = user_id
        /// </summary>
        /// <param name="userId">The ID of the current team member user</param>
        /// <returns>List of assigned tasks</returns>
        [HttpGet("tasks")]
        [ProducesResponseType(typeof(List<TaskResponseDTO>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetMyTasks([FromQuery] int userId)
        {
            try
            {
                var tasks = await _teamMemberService.GetMyTasksAsync(userId);
                return Ok(tasks);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// BR-056: Get task details - Only if task is assigned to the user
        /// Validates that TASK.assigned_to matches user_id
        /// Error: "Access denied. This task is not assigned to you."
        /// </summary>
        /// <param name="userId">The ID of the current team member user</param>
        /// <param name="taskId">The ID of the task</param>
        /// <returns>Task details</returns>
        [HttpGet("tasks/{taskId}")]
        [ProducesResponseType(typeof(TaskResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetMyTask([FromQuery] int userId, int taskId)
        {
            try
            {
                var task = await _teamMemberService.GetMyTaskAsync(userId, taskId);
                if (task == null)
                    return NotFound(new { message = "Task not found" });
                return Ok(task);
            }
            catch (Exception ex)
            {
                // BR-056: Access denied error for unauthorized user
                if (ex.Message.Contains("Access denied"))
                    return Forbid();
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// BR-056: Update task status - Only for assigned tasks
        /// Validates that TASK.assigned_to matches user_id
        /// Error: "Access denied. This task is not assigned to you."
        /// </summary>
        /// <param name="userId">The ID of the current team member user</param>
        /// <param name="taskId">The ID of the task</param>
        /// <param name="dto">Update request</param>
        /// <returns>Updated task</returns>
        [HttpPut("tasks/{taskId}/status")]
        [ProducesResponseType(typeof(TaskResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateTaskStatus([FromQuery] int userId, int taskId, [FromBody] UpdateTaskStatusDTO dto)
        {
            try
            {
                var task = await _teamMemberService.UpdateTaskStatusAsync(userId, taskId, dto);
                return Ok(task);
            }
            catch (Exception ex)
            {
                // BR-056: Access denied error for unauthorized user
                if (ex.Message.Contains("Access denied"))
                    return Forbid();
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// BR-056: Mark task as completed
        /// Validates that TASK.assigned_to matches user_id
        /// Sets CompletedAt timestamp
        /// Error: "Access denied. This task is not assigned to you."
        /// </summary>
        /// <param name="userId">The ID of the current team member user</param>
        /// <param name="taskId">The ID of the task</param>
        /// <returns>Updated task with completion timestamp</returns>
        [HttpPost("tasks/{taskId}/complete")]
        [ProducesResponseType(typeof(TaskResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CompleteTask([FromQuery] int userId, int taskId)
        {
            try
            {
                var task = await _teamMemberService.CompleteTaskAsync(userId, taskId);
                return Ok(task);
            }
            catch (Exception ex)
            {
                // BR-056: Access denied error for unauthorized user
                if (ex.Message.Contains("Access denied"))
                    return Forbid();
                return BadRequest(new { message = ex.Message });
            }
        }

        #endregion

        #region Statistics

        /// <summary>
        /// BR-056: Get personal task statistics
        /// Returns task completion and progress metrics for the current user
        /// </summary>
        /// <param name="userId">The ID of the current team member user</param>
        /// <returns>Personal task statistics</returns>
        [HttpGet("statistics/tasks")]
        [ProducesResponseType(typeof(PersonalTaskStatisticResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetMyTaskStatistics([FromQuery] int userId)
        {
            try
            {
                var statistics = await _teamMemberService.GetMyTaskStatisticsAsync(userId);
                if (statistics == null)
                    return NotFound(new { message = "Task statistics not found" });
                return Ok(statistics);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// BR-056: Get personal commit statistics
        /// Returns commit activity metrics for the current user
        /// </summary>
        /// <param name="userId">The ID of the current team member user</param>
        /// <returns>Personal commit statistics</returns>
        [HttpGet("statistics/commits")]
        [ProducesResponseType(typeof(CommitStatisticResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetMyCommitStatistics([FromQuery] int userId)
        {
            try
            {
                var statistics = await _teamMemberService.GetMyCommitStatisticsAsync(userId);
                if (statistics == null)
                    return NotFound(new { message = "Commit statistics not found" });
                return Ok(statistics);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        #endregion
    }
}
