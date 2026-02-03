using BLL.DTOs.Admin;
using BLL.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace SWP391_JGMS.Controllers
{
    /// <summary>
    /// Lecturer API endpoints with group-scoped access control
    /// BR-054: Lecturer Group-Scoped Access - Lecturers can only access groups assigned to them
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class LecturerController : ControllerBase
    {
        private readonly ILecturerService _lecturerService;

        public LecturerController(ILecturerService lecturerService)
        {
            _lecturerService = lecturerService;
        }

        /// <summary>
        /// BR-054: Get all groups assigned to the current lecturer
        /// Only returns groups where lecturer_id matches current user
        /// </summary>
        /// <param name="lecturerId">The ID of the current lecturer user</param>
        /// <returns>List of groups assigned to the lecturer</returns>
        [HttpGet("groups")]
        [ProducesResponseType(typeof(List<StudentGroupResponseDTO>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMyGroups([FromQuery] int lecturerId)
        {
            try
            {
                var groups = await _lecturerService.GetMyGroupsAsync(lecturerId);
                return Ok(groups);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// BR-054: Get details of a specific group assigned to the lecturer
        /// Validates that lecturer_id in group matches current user
        /// Error: "Access denied. You are not assigned to this group."
        /// </summary>
        /// <param name="lecturerId">The ID of the current lecturer user</param>
        /// <param name="groupId">The ID of the group to retrieve</param>
        /// <returns>Group details if lecturer is assigned</returns>
        [HttpGet("groups/{groupId}")]
        [ProducesResponseType(typeof(StudentGroupResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetGroupById([FromQuery] int lecturerId, int groupId)
        {
            try
            {
                var group = await _lecturerService.GetGroupByIdAsync(lecturerId, groupId);
                if (group == null)
                    return NotFound(new { message = "Group not found" });
                return Ok(group);
            }
            catch (Exception ex)
            {
                // BR-054: Access denied error for unauthorized lecturer
                if (ex.Message.Contains("Access denied"))
                    return Forbid();
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// BR-054: Get all members in a group assigned to the lecturer
        /// Validates lecturer access before retrieving members
        /// Error: "Access denied. You are not assigned to this group."
        /// </summary>
        /// <param name="lecturerId">The ID of the current lecturer user</param>
        /// <param name="groupId">The ID of the group</param>
        /// <returns>List of group members</returns>
        [HttpGet("groups/{groupId}/members")]
        [ProducesResponseType(typeof(List<GroupMemberResponseDTO>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetGroupMembers([FromQuery] int lecturerId, int groupId)
        {
            try
            {
                var members = await _lecturerService.GetGroupMembersAsync(lecturerId, groupId);
                return Ok(members);
            }
            catch (Exception ex)
            {
                // BR-054: Access denied error for unauthorized lecturer
                if (ex.Message.Contains("Access denied"))
                    return Forbid();
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// BR-054: Add a student to a group assigned to the lecturer
        /// Validates lecturer access before adding member
        /// Error: "Access denied. You are not assigned to this group."
        /// </summary>
        /// <param name="lecturerId">The ID of the current lecturer user</param>
        /// <param name="groupId">The ID of the group</param>
        /// <param name="dto">Request containing studentId</param>
        /// <returns>Success message</returns>
        [HttpPost("groups/{groupId}/members")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> AddStudentToGroup([FromQuery] int lecturerId, int groupId, [FromBody] AddStudentToGroupDTO dto)
        {
            try
            {
                await _lecturerService.AddStudentToGroupAsync(lecturerId, groupId, dto.StudentId);
                return Ok(new { message = "Student added to group successfully" });
            }
            catch (Exception ex)
            {
                // BR-054: Access denied error for unauthorized lecturer
                if (ex.Message.Contains("Access denied"))
                    return Forbid();
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// BR-054: Remove a student from a group assigned to the lecturer
        /// Validates lecturer access before removing member
        /// Error: "Access denied. You are not assigned to this group."
        /// </summary>
        /// <param name="lecturerId">The ID of the current lecturer user</param>
        /// <param name="groupId">The ID of the group</param>
        /// <param name="studentId">The ID of the student to remove</param>
        /// <returns>Success message</returns>
        [HttpDelete("groups/{groupId}/members/{studentId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RemoveStudentFromGroup([FromQuery] int lecturerId, int groupId, int studentId)
        {
            try
            {
                await _lecturerService.RemoveStudentFromGroupAsync(lecturerId, groupId, studentId);
                return Ok(new { message = "Student removed from group successfully" });
            }
            catch (Exception ex)
            {
                // BR-054: Access denied error for unauthorized lecturer
                if (ex.Message.Contains("Access denied"))
                    return Forbid();
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// BR-054: Update a group assigned to the lecturer
        /// Validates lecturer access before allowing updates
        /// Error: "Access denied. You are not assigned to this group."
        /// </summary>
        /// <param name="lecturerId">The ID of the current lecturer user</param>
        /// <param name="groupId">The ID of the group to update</param>
        /// <param name="dto">Update request containing group details</param>
        /// <returns>Updated group details</returns>
        [HttpPut("groups/{groupId}")]
        [ProducesResponseType(typeof(StudentGroupResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateGroup([FromQuery] int lecturerId, int groupId, [FromBody] UpdateStudentGroupDTO dto)
        {
            try
            {
                var updatedGroup = await _lecturerService.UpdateGroupAsync(lecturerId, groupId, dto);
                return Ok(updatedGroup);
            }
            catch (Exception ex)
            {
                // BR-054: Access denied error for unauthorized lecturer
                if (ex.Message.Contains("Access denied"))
                    return Forbid();
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
