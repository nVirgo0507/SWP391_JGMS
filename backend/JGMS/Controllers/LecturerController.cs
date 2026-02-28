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
        /// Add a student to a group assigned to the lecturer.
        /// Accepts group code (e.g. "SE1234") or numeric group ID.
        /// </summary>
        [HttpPost("groups/{groupCode}/members")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> AddStudentToGroup(string groupCode, [FromBody] AddStudentToGroupDTO dto)
        {
            try
            {
                var groupId = await _resolver.ResolveGroupIdAsync(groupCode);
                await _lecturerService.AddStudentToGroupAsync(GetCurrentUserId(), groupId, dto.StudentId);
                return Ok(new { message = "Student added to group successfully" });
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
        /// </summary>
        [HttpDelete("groups/{groupCode}/members/{studentId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> RemoveStudentFromGroup(string groupCode, int studentId)
        {
            try
            {
                var groupId = await _resolver.ResolveGroupIdAsync(groupCode);
                await _lecturerService.RemoveStudentFromGroupAsync(GetCurrentUserId(), groupId, studentId);
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
    }
}
