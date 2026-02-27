﻿﻿using BLL.DTOs.Admin;
using BLL.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Security.Claims;

namespace SWP391_JGMS.Controllers
{
    /// <summary>
    /// Admin API endpoints — user management, group management, and integration configuration.
    /// All endpoints require admin role authentication via JWT.
    /// </summary>
    [ApiController]
	[Authorize(Roles = "admin")]
	[Route("api/admin")]
    [Produces("application/json")]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _adminService;
        private readonly IIntegrationService _integrationService;

        public AdminController(IAdminService adminService, IIntegrationService integrationService)
        {
            _adminService = adminService;
            _integrationService = integrationService;
        }

        /// <summary>Reads the authenticated user's ID from the JWT sub claim.</summary>
        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (int.TryParse(userIdClaim, out var id)) return id;
            throw new UnauthorizedAccessException("Invalid or missing user identity in token.");
        }

        #region User Management

        /// <summary>
        /// BR-053: Admin Full Access - Create a new user (admin, lecturer, or student)
        /// BR-001: Unique Email Address
        /// BR-005: Password Strength
        /// BR-006: Active Status Default
        /// </summary>
        [HttpPost("users")]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserDTO dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var user = await _adminService.CreateUserAsync(dto);
                return CreatedAtAction(nameof(GetUserById), new { userId = user.UserId }, user);
            }
            catch (Exception ex)
            {
                var errorMessage = ex.Message;
                if (ex.InnerException != null)
                {
                    errorMessage += " | Inner: " + ex.InnerException.Message;
                }
                return BadRequest(new { message = errorMessage, stackTrace = ex.StackTrace });
            }
        }

        /// <summary>
        /// BR-053: Admin Full Access - Update user information
        /// BR-060: Preserve Audit Trail
        /// </summary>
        [HttpPut("users/{userId}")]
        public async Task<IActionResult> UpdateUser(int userId, [FromBody] UpdateUserDTO dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var user = await _adminService.UpdateUserAsync(userId, dto);
                return Ok(user);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// BR-053: Admin Full Access - Get user by ID
        /// </summary>
        [HttpGet("users/{userId}")]
        public async Task<IActionResult> GetUserById(int userId)
        {
            try
            {
                var user = await _adminService.GetUserByIdAsync(userId);
                if (user == null)
                {
                    return NotFound(new { message = "User not found" });
                }
                return Ok(user);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// BR-053: Admin Full Access - Get all users
        /// </summary>
        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers()
        {
            try
            {
                var users = await _adminService.GetAllUsersAsync();
                return Ok(users);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// BR-053: Admin Full Access - Get users by role
        /// </summary>
        [HttpGet("users/role/{role}")]
        public async Task<IActionResult> GetUsersByRole(string role)
        {
            try
            {
                var users = await _adminService.GetUsersByRoleAsync(role);
                return Ok(users);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// BR-053: Admin Full Access - Delete user
        /// BR-059: Cascade Delete Prevention
        /// </summary>
        [HttpDelete("users/{userId}")]
        public async Task<IActionResult> DeleteUser(int userId)
        {
            try
            {
                await _adminService.DeleteUserAsync(userId);
                return Ok(new { message = "User deleted successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// BR-053: Admin Full Access - Set user status
        /// BR-007: Inactive Users Cannot Login
        /// </summary>
        [HttpPatch("users/{userId}/status")]
        public async Task<IActionResult> SetUserStatus(int userId, [FromBody] SetUserStatusDTO dto)
        {
            try
            {
                await _adminService.SetUserStatusAsync(userId, dto.Status);
                return Ok(new { message = "User status updated successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        #endregion

        #region Student Group Management

        /// <summary>
        /// BR-053: Admin Full Access - Create a new student group
        /// </summary>
        [HttpPost("groups")]
        public async Task<IActionResult> CreateStudentGroup([FromBody] CreateStudentGroupDTO dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var group = await _adminService.CreateStudentGroupAsync(dto);
                return CreatedAtAction(nameof(GetStudentGroupById), new { groupId = group.GroupId }, group);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// BR-053: Admin Full Access - Update student group
        /// BR-060: Preserve Audit Trail
        /// </summary>
        [HttpPut("groups/{groupId}")]
        public async Task<IActionResult> UpdateStudentGroup(int groupId, [FromBody] UpdateStudentGroupDTO dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var group = await _adminService.UpdateStudentGroupAsync(groupId, dto);
                return Ok(group);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// BR-053: Admin Full Access - Get student group by ID
        /// </summary>
        [HttpGet("groups/{groupId}")]
        public async Task<IActionResult> GetStudentGroupById(int groupId)
        {
            try
            {
                var group = await _adminService.GetStudentGroupByIdAsync(groupId);
                if (group == null)
                {
                    return NotFound(new { message = "Student group not found" });
                }
                return Ok(group);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// BR-053: Admin Full Access - Get all student groups
        /// </summary>
        [HttpGet("groups")]
        public async Task<IActionResult> GetAllStudentGroups()
        {
            try
            {
                var groups = await _adminService.GetAllStudentGroupsAsync();
                return Ok(groups);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// BR-053: Admin Full Access - Delete student group
        /// BR-061: Soft Delete for Groups
        /// </summary>
        [HttpDelete("groups/{groupId}")]
        public async Task<IActionResult> DeleteStudentGroup(int groupId)
        {
            try
            {
                await _adminService.DeleteStudentGroupAsync(groupId);
                return Ok(new { message = "Student group deleted successfully (soft delete)" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        #endregion

        #region Lecturer Management

        /// <summary>
        /// BR-053: Admin Full Access - Get all lecturers
        /// </summary>
        [HttpGet("lecturers")]
        public async Task<IActionResult> GetAllLecturers()
        {
            try
            {
                var lecturers = await _adminService.GetAllLecturersAsync();
                return Ok(lecturers);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// BR-053: Admin Full Access - Assign lecturer to group
        /// </summary>
        [HttpPut("groups/{groupId}/lecturer")]
        public async Task<IActionResult> AssignLecturerToGroup(int groupId, [FromBody] AssignLecturerDTO dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                await _adminService.AssignLecturerToGroupAsync(groupId, dto.LecturerId);
                return Ok(new { message = "Lecturer assigned to group successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        #endregion

        #region Group Member Management

        /// <summary>
        /// BR-053: Admin Full Access - Add student to group
        /// </summary>
        [HttpPost("groups/{groupId}/members/{studentId}")]
        public async Task<IActionResult> AddStudentToGroup(int groupId, int studentId)
        {
            try
            {
                await _adminService.AddStudentToGroupAsync(groupId, studentId);
                return Ok(new { message = "Student added to group successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// BR-053: Admin Full Access - Remove student from group
        /// </summary>
        [HttpDelete("groups/{groupId}/members/{studentId}")]
        public async Task<IActionResult> RemoveStudentFromGroup(int groupId, int studentId)
        {
            try
            {
                await _adminService.RemoveStudentFromGroupAsync(groupId, studentId);
                return Ok(new { message = "Student removed from group successfully" });
            }
            catch (Exception ex)
            {
                var errorMessage = ex.Message;
                if (ex.InnerException != null)
                {
                    errorMessage += " | Inner: " + ex.InnerException.Message;
                }
                return BadRequest(new { message = errorMessage });
            }
        }

        #endregion

        #region User Integration Management (GitHub & Jira)

        /// <summary>
        /// Configure GitHub integration for a user — sets their github_username.
        /// </summary>
        [HttpPost("users/{targetUserId}/github")]
        [ProducesResponseType(typeof(UserResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ConfigureGithub(int targetUserId, [FromBody] GitHubConfigureRequest request)
        {
            try
            {
                var user = await _integrationService.ConfigureGithubAsync(GetCurrentUserId(), targetUserId, request.GithubUsername);
                return Ok(user);
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Remove GitHub integration for a user — clears their github_username.
        /// </summary>
        [HttpDelete("users/{targetUserId}/github")]
        [ProducesResponseType(typeof(UserResponseDTO), StatusCodes.Status200OK)]
        public async Task<IActionResult> RemoveGithub(int targetUserId)
        {
            try
            {
                var user = await _integrationService.RemoveGithubAsync(GetCurrentUserId(), targetUserId);
                return Ok(user);
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Configure Jira integration for a user — sets their jira_account_id.
        /// </summary>
        [HttpPost("users/{targetUserId}/jira")]
        [ProducesResponseType(typeof(UserResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ConfigureJira(int targetUserId, [FromBody] JiraConfigureRequest request)
        {
            try
            {
                var user = await _integrationService.ConfigureJiraAsync(GetCurrentUserId(), targetUserId, request.JiraAccountId);
                return Ok(user);
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Remove Jira integration for a user — clears their jira_account_id.
        /// </summary>
        [HttpDelete("users/{targetUserId}/jira")]
        [ProducesResponseType(typeof(UserResponseDTO), StatusCodes.Status200OK)]
        public async Task<IActionResult> RemoveJira(int targetUserId)
        {
            try
            {
                var user = await _integrationService.RemoveJiraAsync(GetCurrentUserId(), targetUserId);
                return Ok(user);
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Get all users with their GitHub/Jira integration status.
        /// </summary>
        [HttpGet("integrations")]
        [ProducesResponseType(typeof(List<IntegrationStatusDTO>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllIntegrations()
        {
            try
            {
                var integrations = await _integrationService.GetAllIntegrationsAsync(GetCurrentUserId());
                return Ok(integrations);
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Test integration connectivity (GitHub or Jira).
        /// Pass integrationType = "GitHub" or "Jira".
        /// </summary>
        [HttpPost("integrations/test")]
        [ProducesResponseType(typeof(IntegrationTestResultDTO), StatusCodes.Status200OK)]
        public async Task<IActionResult> TestIntegration([FromQuery] string integrationType)
        {
            try
            {
                var result = await _integrationService.TestIntegrationAsync(GetCurrentUserId(), integrationType);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        #endregion
    }
}
