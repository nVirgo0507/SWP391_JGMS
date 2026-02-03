using BLL.DTOs.Admin;
using BLL.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace SWP391_JGMS.Controllers
{
    /// <summary>
    /// Team Leader API endpoints with group-scoped access control
    /// BR-055: Team Leader Group-Scoped Access - Team leaders can only manage their own group's project
    /// Validation: Check user is leader of the group via GROUP_MEMBER.is_leader
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class TeamLeaderController : ControllerBase
    {
        private readonly ITeamLeaderService _teamLeaderService;

        public TeamLeaderController(ITeamLeaderService teamLeaderService)
        {
            _teamLeaderService = teamLeaderService;
        }

        #region Project Management

        /// <summary>
        /// BR-055: Get project details for the leader's group
        /// Validates that user is leader of the group
        /// Error: "Access denied. You are not the leader of this group."
        /// </summary>
        /// <param name="userId">The ID of the current team leader user</param>
        /// <param name="groupId">The ID of the group</param>
        /// <returns>Project details</returns>
        [HttpGet("groups/{groupId}/project")]
        [ProducesResponseType(typeof(ProjectResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetGroupProject([FromQuery] int userId, int groupId)
        {
            try
            {
                var project = await _teamLeaderService.GetGroupProjectAsync(userId, groupId);
                if (project == null)
                    return NotFound(new { message = "Project not found" });
                return Ok(project);
            }
            catch (Exception ex)
            {
                // BR-055: Access denied error for unauthorized leader
                if (ex.Message.Contains("Access denied"))
                    return Forbid();
                return BadRequest(new { message = ex.Message });
            }
        }

        #endregion

        #region Requirements Management

        /// <summary>
        /// BR-055: Get all requirements for the leader's group project
        /// Validates that user is leader of the group
        /// Error: "Access denied. You are not the leader of this group."
        /// </summary>
        /// <param name="userId">The ID of the current team leader user</param>
        /// <param name="groupId">The ID of the group</param>
        /// <returns>List of requirements</returns>
        [HttpGet("groups/{groupId}/requirements")]
        [ProducesResponseType(typeof(List<RequirementResponseDTO>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetGroupRequirements([FromQuery] int userId, int groupId)
        {
            try
            {
                var requirements = await _teamLeaderService.GetGroupRequirementsAsync(userId, groupId);
                return Ok(requirements);
            }
            catch (Exception ex)
            {
                // BR-055: Access denied error for unauthorized leader
                if (ex.Message.Contains("Access denied"))
                    return Forbid();
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// BR-055: Create a requirement for the leader's group
        /// Validates that user is leader of the group
        /// Error: "Access denied. You are not the leader of this group."
        /// </summary>
        /// <param name="userId">The ID of the current team leader user</param>
        /// <param name="groupId">The ID of the group</param>
        /// <param name="dto">Request containing requirement details</param>
        /// <returns>Created requirement</returns>
        [HttpPost("groups/{groupId}/requirements")]
        [ProducesResponseType(typeof(RequirementResponseDTO), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateRequirement([FromQuery] int userId, int groupId, [FromBody] CreateRequirementDTO dto)
        {
            try
            {
                var requirement = await _teamLeaderService.CreateRequirementAsync(userId, groupId, dto);
                return CreatedAtAction(nameof(GetGroupRequirements), requirement);
            }
            catch (Exception ex)
            {
                // BR-055: Access denied error for unauthorized leader
                if (ex.Message.Contains("Access denied"))
                    return Forbid();
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// BR-055: Update a requirement for the leader's group
        /// Validates that user is leader of the group
        /// Error: "Access denied. You are not the leader of this group."
        /// </summary>
        /// <param name="userId">The ID of the current team leader user</param>
        /// <param name="groupId">The ID of the group</param>
        /// <param name="requirementId">The ID of the requirement to update</param>
        /// <param name="dto">Update request</param>
        /// <returns>Updated requirement</returns>
        [HttpPut("groups/{groupId}/requirements/{requirementId}")]
        [ProducesResponseType(typeof(RequirementResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateRequirement([FromQuery] int userId, int groupId, int requirementId, [FromBody] UpdateRequirementDTO dto)
        {
            try
            {
                var requirement = await _teamLeaderService.UpdateRequirementAsync(userId, groupId, requirementId, dto);
                return Ok(requirement);
            }
            catch (Exception ex)
            {
                // BR-055: Access denied error for unauthorized leader
                if (ex.Message.Contains("Access denied"))
                    return Forbid();
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// BR-055: Delete a requirement for the leader's group
        /// Validates that user is leader of the group
        /// Error: "Access denied. You are not the leader of this group."
        /// </summary>
        /// <param name="userId">The ID of the current team leader user</param>
        /// <param name="groupId">The ID of the group</param>
        /// <param name="requirementId">The ID of the requirement to delete</param>
        /// <returns>Success message</returns>
        [HttpDelete("groups/{groupId}/requirements/{requirementId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DeleteRequirement([FromQuery] int userId, int groupId, int requirementId)
        {
            try
            {
                await _teamLeaderService.DeleteRequirementAsync(userId, groupId, requirementId);
                return Ok(new { message = "Requirement deleted successfully" });
            }
            catch (Exception ex)
            {
                // BR-055: Access denied error for unauthorized leader
                if (ex.Message.Contains("Access denied"))
                    return Forbid();
                return BadRequest(new { message = ex.Message });
            }
        }

        #endregion

        #region Tasks Management

        /// <summary>
        /// BR-055: Get all tasks for the leader's group
        /// Validates that user is leader of the group
        /// Error: "Access denied. You are not the leader of this group."
        /// </summary>
        /// <param name="userId">The ID of the current team leader user</param>
        /// <param name="groupId">The ID of the group</param>
        /// <returns>List of tasks</returns>
        [HttpGet("groups/{groupId}/tasks")]
        [ProducesResponseType(typeof(List<TaskResponseDTO>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetGroupTasks([FromQuery] int userId, int groupId)
        {
            try
            {
                var tasks = await _teamLeaderService.GetGroupTasksAsync(userId, groupId);
                return Ok(tasks);
            }
            catch (Exception ex)
            {
                // BR-055: Access denied error for unauthorized leader
                if (ex.Message.Contains("Access denied"))
                    return Forbid();
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// BR-055: Create a task for the leader's group
        /// Validates that user is leader of the group
        /// Error: "Access denied. You are not the leader of this group."
        /// </summary>
        /// <param name="userId">The ID of the current team leader user</param>
        /// <param name="groupId">The ID of the group</param>
        /// <param name="dto">Request containing task details</param>
        /// <returns>Created task</returns>
        [HttpPost("groups/{groupId}/tasks")]
        [ProducesResponseType(typeof(TaskResponseDTO), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateTask([FromQuery] int userId, int groupId, [FromBody] CreateTaskDTO dto)
        {
            try
            {
                var task = await _teamLeaderService.CreateTaskAsync(userId, groupId, dto);
                return CreatedAtAction(nameof(GetGroupTasks), task);
            }
            catch (Exception ex)
            {
                // BR-055: Access denied error for unauthorized leader
                if (ex.Message.Contains("Access denied"))
                    return Forbid();
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// BR-055: Update a task for the leader's group
        /// Validates that user is leader of the group
        /// Error: "Access denied. You are not the leader of this group."
        /// </summary>
        /// <param name="userId">The ID of the current team leader user</param>
        /// <param name="groupId">The ID of the group</param>
        /// <param name="taskId">The ID of the task to update</param>
        /// <param name="dto">Update request</param>
        /// <returns>Updated task</returns>
        [HttpPut("groups/{groupId}/tasks/{taskId}")]
        [ProducesResponseType(typeof(TaskResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateTask([FromQuery] int userId, int groupId, int taskId, [FromBody] UpdateTaskDTO dto)
        {
            try
            {
                var task = await _teamLeaderService.UpdateTaskAsync(userId, groupId, taskId, dto);
                return Ok(task);
            }
            catch (Exception ex)
            {
                // BR-055: Access denied error for unauthorized leader
                if (ex.Message.Contains("Access denied"))
                    return Forbid();
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// BR-055: Assign task to team member
        /// Validates that user is leader of the group
        /// Error: "Access denied. You are not the leader of this group."
        /// </summary>
        /// <param name="userId">The ID of the current team leader user</param>
        /// <param name="groupId">The ID of the group</param>
        /// <param name="taskId">The ID of the task</param>
        /// <param name="dto">Request containing memberId</param>
        /// <returns>Success message</returns>
        [HttpPost("groups/{groupId}/tasks/{taskId}/assign")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> AssignTask([FromQuery] int userId, int groupId, int taskId, [FromBody] AssignTaskDTO dto)
        {
            try
            {
                await _teamLeaderService.AssignTaskAsync(userId, groupId, taskId, dto.MemberId);
                return Ok(new { message = "Task assigned successfully" });
            }
            catch (Exception ex)
            {
                // BR-055: Access denied error for unauthorized leader
                if (ex.Message.Contains("Access denied"))
                    return Forbid();
                return BadRequest(new { message = ex.Message });
            }
        }

        #endregion

        #region SRS Document Management

        /// <summary>
        /// BR-055: Get SRS document for the leader's group
        /// Validates that user is leader of the group
        /// Error: "Access denied. You are not the leader of this group."
        /// </summary>
        /// <param name="userId">The ID of the current team leader user</param>
        /// <param name="groupId">The ID of the group</param>
        /// <returns>SRS document details</returns>
        [HttpGet("groups/{groupId}/srs")]
        [ProducesResponseType(typeof(SrsDocumentResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetGroupSrsDocument([FromQuery] int userId, int groupId)
        {
            try
            {
                var srs = await _teamLeaderService.GetGroupSrsDocumentAsync(userId, groupId);
                if (srs == null)
                    return NotFound(new { message = "SRS document not found" });
                return Ok(srs);
            }
            catch (Exception ex)
            {
                // BR-055: Access denied error for unauthorized leader
                if (ex.Message.Contains("Access denied"))
                    return Forbid();
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// BR-055: Create SRS document for the leader's group
        /// Validates that user is leader of the group
        /// Error: "Access denied. You are not the leader of this group."
        /// </summary>
        /// <param name="userId">The ID of the current team leader user</param>
        /// <param name="groupId">The ID of the group</param>
        /// <param name="dto">Request containing SRS details</param>
        /// <returns>Created SRS document</returns>
        [HttpPost("groups/{groupId}/srs")]
        [ProducesResponseType(typeof(SrsDocumentResponseDTO), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateSrsDocument([FromQuery] int userId, int groupId, [FromBody] CreateSrsDocumentDTO dto)
        {
            try
            {
                var srs = await _teamLeaderService.CreateSrsDocumentAsync(userId, groupId, dto);
                return CreatedAtAction(nameof(GetGroupSrsDocument), srs);
            }
            catch (Exception ex)
            {
                // BR-055: Access denied error for unauthorized leader
                if (ex.Message.Contains("Access denied"))
                    return Forbid();
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// BR-055: Update SRS document for the leader's group
        /// Validates that user is leader of the group
        /// Error: "Access denied. You are not the leader of this group."
        /// </summary>
        /// <param name="userId">The ID of the current team leader user</param>
        /// <param name="groupId">The ID of the group</param>
        /// <param name="srsId">The ID of the SRS document to update</param>
        /// <param name="dto">Update request</param>
        /// <returns>Updated SRS document</returns>
        [HttpPut("groups/{groupId}/srs/{srsId}")]
        [ProducesResponseType(typeof(SrsDocumentResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateSrsDocument([FromQuery] int userId, int groupId, int srsId, [FromBody] UpdateSrsDocumentDTO dto)
        {
            try
            {
                var srs = await _teamLeaderService.UpdateSrsDocumentAsync(userId, groupId, srsId, dto);
                return Ok(srs);
            }
            catch (Exception ex)
            {
                // BR-055: Access denied error for unauthorized leader
                if (ex.Message.Contains("Access denied"))
                    return Forbid();
                return BadRequest(new { message = ex.Message });
            }
        }

        #endregion
    }
}
