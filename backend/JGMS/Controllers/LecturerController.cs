using BLL.DTOs.Admin;
using BLL.Helpers;
using BLL.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Security.Claims;

namespace SWP391_JGMS.Controllers
{
    /// <summary>
    /// Lecturer API endpoints with group-scoped access control.
    /// Group endpoints accept a group code (e.g. "SE1234") or numeric group ID.
    /// </summary>
    [ApiController]
    [Authorize(Roles = "lecturer")]
    [Route("api/lecturers")]
    [Produces("application/json")]
    public class LecturerController : ControllerBase
    {
        private readonly ILecturerService _lecturerService;
        private readonly IdentifierResolver _resolver;

        public LecturerController(ILecturerService lecturerService, IdentifierResolver resolver)
        {
            _lecturerService = lecturerService;
            _resolver = resolver;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (int.TryParse(userIdClaim, out var id)) return id;
            throw new UnauthorizedAccessException("Invalid or missing user identity in token.");
        }

        /// <summary>
        /// Get all groups assigned to the current lecturer.
        /// </summary>
        [HttpGet("groups")]
        [ProducesResponseType(typeof(List<StudentGroupResponseDTO>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMyGroups()
        {
            try
            {
                var groups = await _lecturerService.GetMyGroupsAsync(GetCurrentUserId());
                return Ok(groups);
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Get details of a specific group assigned to the lecturer.
        /// Accepts group code (e.g. "SE1234") or numeric group ID.
        /// </summary>
        [HttpGet("groups/{groupCode}")]
        [ProducesResponseType(typeof(StudentGroupResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetGroupById(string groupCode)
        {
            try
            {
                var groupId = await _resolver.ResolveGroupIdAsync(groupCode);
                var group = await _lecturerService.GetGroupByIdAsync(GetCurrentUserId(), groupId);
                if (group == null)
                    return NotFound(new { message = "Group not found" });
                return Ok(group);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Access denied"))
                    return StatusCode(403, new { message = ex.Message });
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Get all members in a group assigned to the lecturer.
        /// Accepts group code (e.g. "SE1234") or numeric group ID.
        /// </summary>
        [HttpGet("groups/{groupCode}/members")]
        [ProducesResponseType(typeof(List<GroupMemberResponseDTO>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetGroupMembers(string groupCode)
        {
            try
            {
                var groupId = await _resolver.ResolveGroupIdAsync(groupCode);
                var members = await _lecturerService.GetGroupMembersAsync(GetCurrentUserId(), groupId);
                return Ok(members);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Access denied"))
                    return StatusCode(403, new { message = ex.Message });
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Add one or more students to a group assigned to the lecturer.
        /// Accepts group code (e.g. "SE1234") or numeric group ID.
        /// Each student identifier can be an email address or a numeric user ID.
        /// Returns a summary of added students and any failures (with reasons).
        /// </summary>
        [HttpPost("groups/{groupCode}/members")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> AddStudentsToGroup(string groupCode, [FromBody] AddStudentsToGroupDTO dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var groupId = await _resolver.ResolveGroupIdAsync(groupCode);
                var result = await _lecturerService.AddStudentsToGroupAsync(GetCurrentUserId(), groupId, dto.StudentIdentifiers);

                if (result.FailureCount > 0 && result.SuccessCount == 0)
                    return BadRequest(result);
                if (result.FailureCount > 0)
                    return StatusCode(207, result);

                return Ok(result);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Access denied"))
                    return StatusCode(403, new { message = ex.Message });
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Remove a student from a group assigned to the lecturer.
        /// Accepts group code (e.g. "SE1234") or numeric group ID.
        /// Accepts student numeric user ID or email address.
        /// </summary>
        [HttpDelete("groups/{groupCode}/members/{studentIdentifier}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> RemoveStudentFromGroup(string groupCode, string studentIdentifier)
        {
            try
            {
                var groupId = await _resolver.ResolveGroupIdAsync(groupCode);
                await _lecturerService.RemoveStudentFromGroupAsync(GetCurrentUserId(), groupId, studentIdentifier);
                return Ok(new { message = "Student removed from group successfully" });
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Access denied"))
                    return StatusCode(403, new { message = ex.Message });
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Update a group assigned to the lecturer.
        /// Accepts group code (e.g. "SE1234") or numeric group ID.
        /// </summary>
        [HttpPut("groups/{groupCode}")]
        [ProducesResponseType(typeof(StudentGroupResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateGroup(string groupCode, [FromBody] UpdateStudentGroupDTO dto)
        {
            try
            {
                var groupId = await _resolver.ResolveGroupIdAsync(groupCode);
                var updatedGroup = await _lecturerService.UpdateGroupAsync(GetCurrentUserId(), groupId, dto);
                return Ok(updatedGroup);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Access denied"))
                    return StatusCode(403, new { message = ex.Message });
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Get all requirements for the assigned group's project.
        /// Accepts group code (e.g. "SE1234") or numeric group ID.
        /// </summary>
        [HttpGet("groups/{groupCode}/requirements")]
        [ProducesResponseType(typeof(List<RequirementResponseDTO>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetGroupRequirements(string groupCode)
        {
            try
            {
                var groupId = await _resolver.ResolveGroupIdAsync(groupCode);
                var requirements = await _lecturerService.GetGroupRequirementsAsync(GetCurrentUserId(), groupId);
                return Ok(requirements);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Access denied"))
                    return StatusCode(403, new { message = ex.Message });
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Get all tasks for the assigned group's project.
        /// Accepts group code (e.g. "SE1234") or numeric group ID.
        /// </summary>
        [HttpGet("groups/{groupCode}/tasks")]
        [ProducesResponseType(typeof(List<TaskResponseDTO>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetGroupTasks(string groupCode)
        {
            try
            {
                var groupId = await _resolver.ResolveGroupIdAsync(groupCode);
                var tasks = await _lecturerService.GetGroupTasksAsync(GetCurrentUserId(), groupId);
                return Ok(tasks);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Access denied"))
                    return StatusCode(403, new { message = ex.Message });
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Get progress reports for the assigned group's project.
        /// Accepts group code (e.g. "SE1234") or numeric group ID.
        /// </summary>
        [HttpGet("groups/{groupCode}/progress-reports")]
        [ProducesResponseType(typeof(List<ProgressReportResponseDTO>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetProjectProgressReports(string groupCode)
        {
            try
            {
                var groupId = await _resolver.ResolveGroupIdAsync(groupCode);
                var reports = await _lecturerService.GetProjectProgressReportsAsync(GetCurrentUserId(), groupId);
                return Ok(reports);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Access denied"))
                    return StatusCode(403, new { message = ex.Message });
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Get aggregated GitHub commit statistics for the assigned group's project.
        /// Accepts group code (e.g. "SE1234") or numeric group ID.
        /// </summary>
        [HttpGet("groups/{groupCode}/commit-statistics")]
        [ProducesResponseType(typeof(GroupCommitStatisticsResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetGithubCommitStatistics(string groupCode)
        {
            try
            {
                var groupId = await _resolver.ResolveGroupIdAsync(groupCode);
                var stats = await _lecturerService.GetGithubCommitStatisticsAsync(GetCurrentUserId(), groupId);
                return Ok(stats);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Access denied"))
                    return StatusCode(403, new { message = ex.Message });
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
