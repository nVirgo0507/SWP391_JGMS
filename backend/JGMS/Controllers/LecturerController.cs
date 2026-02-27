using BLL.DTOs.Admin;
using BLL.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Security.Claims;

namespace SWP391_JGMS.Controllers
{
    /// <summary>
    /// Lecturer API endpoints with group-scoped access control
    /// BR-054: Lecturer Group-Scoped Access - Lecturers can only access groups assigned to them
    /// </summary>
    [ApiController]
    [Authorize(Roles = "lecturer")]
    [Route("api/lecturers")]
    [Produces("application/json")]
    public class LecturerController : ControllerBase
    {
        private readonly ILecturerService _lecturerService;

        public LecturerController(ILecturerService lecturerService)
        {
            _lecturerService = lecturerService;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (int.TryParse(userIdClaim, out var id)) return id;
            throw new UnauthorizedAccessException("Invalid or missing user identity in token.");
        }

        /// <summary>
        /// BR-054: Get all groups assigned to the current lecturer
        /// </summary>
        /// <returns>List of groups assigned to the lecturer</returns>
        [HttpGet("groups")]
        [ProducesResponseType(typeof(List<StudentGroupResponseDTO>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
        /// BR-054: Get details of a specific group assigned to the lecturer
        /// </summary>
        /// <param name="groupId">The ID of the group to retrieve</param>
        /// <returns>Group details if lecturer is assigned</returns>
        [HttpGet("groups/{groupId}")]
        [ProducesResponseType(typeof(StudentGroupResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetGroupById(int groupId)
        {
            try
            {
                var group = await _lecturerService.GetGroupByIdAsync(GetCurrentUserId(), groupId);
                if (group == null)
                    return NotFound(new { message = "Group not found" });
                return Ok(group);
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
        /// BR-054: Get all members in a group assigned to the lecturer
        /// </summary>
        /// <param name="groupId">The ID of the group</param>
        /// <returns>List of group members</returns>
        [HttpGet("groups/{groupId}/members")]
        [ProducesResponseType(typeof(List<GroupMemberResponseDTO>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetGroupMembers(int groupId)
        {
            try
            {
                var members = await _lecturerService.GetGroupMembersAsync(GetCurrentUserId(), groupId);
                return Ok(members);
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
        /// BR-054: Add a student to a group assigned to the lecturer
        /// </summary>
        /// <param name="groupId">The ID of the group</param>
        /// <param name="dto">Request containing studentId</param>
        /// <returns>Success message</returns>
        [HttpPost("groups/{groupId}/members")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> AddStudentToGroup(int groupId, [FromBody] AddStudentToGroupDTO dto)
        {
            try
            {
                await _lecturerService.AddStudentToGroupAsync(GetCurrentUserId(), groupId, dto.StudentId);
                return Ok(new { message = "Student added to group successfully" });
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
        /// BR-054: Remove a student from a group assigned to the lecturer
        /// </summary>
        /// <param name="groupId">The ID of the group</param>
        /// <param name="studentId">The ID of the student to remove</param>
        /// <returns>Success message</returns>
        [HttpDelete("groups/{groupId}/members/{studentId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RemoveStudentFromGroup(int groupId, int studentId)
        {
            try
            {
                await _lecturerService.RemoveStudentFromGroupAsync(GetCurrentUserId(), groupId, studentId);
                return Ok(new { message = "Student removed from group successfully" });
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
        /// BR-054: Update a group assigned to the lecturer
        /// </summary>
        /// <param name="groupId">The ID of the group to update</param>
        /// <param name="dto">Update request containing group details</param>
        /// <returns>Updated group details</returns>
        [HttpPut("groups/{groupId}")]
        [ProducesResponseType(typeof(StudentGroupResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateGroup(int groupId, [FromBody] UpdateStudentGroupDTO dto)
        {
            try
            {
                var updatedGroup = await _lecturerService.UpdateGroupAsync(GetCurrentUserId(), groupId, dto);
                return Ok(updatedGroup);
            }
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
