using BLL.DTOs.Admin;
using BLL.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Security.Claims;

namespace SWP391_JGMS.Controllers
{
    /// <summary>
    /// Team Member API endpoints with self-scoped and read-only access control
    /// BR-056: Team members can only update their own assigned tasks
    /// BR-057: Team members can view requirements but cannot create/edit/delete
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/team-member")]
    [Produces("application/json")]
    public class TeamMemberController : ControllerBase
    {
        private readonly ITeamMemberService _teamMemberService;

        public TeamMemberController(ITeamMemberService teamMemberService)
        {
            _teamMemberService = teamMemberService;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (int.TryParse(userIdClaim, out var id)) return id;
            throw new UnauthorizedAccessException("Invalid or missing user identity in token.");
        }

        #region Requirements Management (Read-Only)

        /// <summary>
        /// BR-057: Get all requirements for the team member's group (read-only)
        /// </summary>
        [HttpGet("groups/{groupId}/requirements")]
        [ProducesResponseType(typeof(List<RequirementResponseDTO>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetGroupRequirements(int groupId)
        {
            try
            {
                var requirements = await _teamMemberService.GetGroupRequirementsAsync(GetCurrentUserId(), groupId);
                return Ok(requirements);
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Access denied"))
                    return StatusCode(403, new { message = ex.Message });
                return BadRequest(new { message = ex.Message });
            }
        }

        #endregion

        #region Task Management

        /// <summary>
        /// BR-056: Get all tasks assigned to the current team member
        /// </summary>
        [HttpGet("tasks")]
        [ProducesResponseType(typeof(List<TaskResponseDTO>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMyTasks()
        {
            try
            {
                var tasks = await _teamMemberService.GetMyTasksAsync(GetCurrentUserId());
                return Ok(tasks);
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// BR-056: Get task details — only if assigned to you
        /// </summary>
        [HttpGet("tasks/{taskId}")]
        [ProducesResponseType(typeof(TaskResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetMyTask(int taskId)
        {
            try
            {
                var task = await _teamMemberService.GetMyTaskAsync(GetCurrentUserId(), taskId);
                if (task == null)
                    return NotFound(new { message = "Task not found" });
                return Ok(task);
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Access denied"))
                    return StatusCode(403, new { message = ex.Message });
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// BR-056: Update task status — only for tasks assigned to you
        /// </summary>
        [HttpPut("tasks/{taskId}/status")]
        [ProducesResponseType(typeof(TaskResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateTaskStatus(int taskId, [FromBody] UpdateTaskStatusDTO dto)
        {
            try
            {
                var task = await _teamMemberService.UpdateTaskStatusAsync(GetCurrentUserId(), taskId, dto);
                return Ok(task);
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Access denied"))
                    return StatusCode(403, new { message = ex.Message });
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// BR-056: Mark task as completed — sets CompletedAt timestamp
        /// </summary>
        [HttpPost("tasks/{taskId}/complete")]
        [ProducesResponseType(typeof(TaskResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CompleteTask(int taskId)
        {
            try
            {
                var task = await _teamMemberService.CompleteTaskAsync(GetCurrentUserId(), taskId);
                return Ok(task);
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Access denied"))
                    return StatusCode(403, new { message = ex.Message });
                return BadRequest(new { message = ex.Message });
            }
        }

        #endregion

        #region Statistics

        /// <summary>
        /// Get personal task statistics (completion and progress metrics)
        /// </summary>
        [HttpGet("statistics/tasks")]
        [ProducesResponseType(typeof(PersonalTaskStatisticResponseDTO), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMyTaskStatistics()
        {
            try
            {
                var statistics = await _teamMemberService.GetMyTaskStatisticsAsync(GetCurrentUserId());
                if (statistics == null)
                    return NotFound(new { message = "Task statistics not found" });
                return Ok(statistics);
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Get personal commit statistics
        /// </summary>
        [HttpGet("statistics/commits")]
        [ProducesResponseType(typeof(CommitStatisticResponseDTO), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMyCommitStatistics()
        {
            try
            {
                var statistics = await _teamMemberService.GetMyCommitStatisticsAsync(GetCurrentUserId());
                if (statistics == null)
                    return NotFound(new { message = "Commit statistics not found" });
                return Ok(statistics);
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        #endregion
    }
}
