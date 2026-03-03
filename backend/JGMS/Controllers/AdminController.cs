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
    /// Admin API endpoints — user management, group management, and integration configuration.
    /// All endpoints require admin role authentication via JWT.
    ///
    /// User endpoints accept email or numeric user ID.
    /// Group endpoints accept group code (e.g. "SE1234") or numeric group ID.
    /// </summary>
    [ApiController]
	[Authorize(Roles = "admin")]
	[Route("api/admin")]
    [Produces("application/json")]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _adminService;
        private readonly IIntegrationService _integrationService;
        private readonly IdentifierResolver _resolver;

        public AdminController(IAdminService adminService, IIntegrationService integrationService, IdentifierResolver resolver)
        {
            _adminService = adminService;
            _integrationService = integrationService;
            _resolver = resolver;
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
        /// Create a new user (admin, lecturer, or student).
        /// </summary>
        [HttpPost("users")]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserDTO dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var user = await _adminService.CreateUserAsync(dto);
                return CreatedAtAction(nameof(GetUserById), new { userIdentifier = user.UserId.ToString() }, user);
            }
            catch (Exception ex)
            {
                var errorMessage = ex.Message;
                if (ex.InnerException != null)
                    errorMessage += " | Inner: " + ex.InnerException.Message;
                return BadRequest(new { message = errorMessage, stackTrace = ex.StackTrace });
            }
        }

        /// <summary>
        /// Update user information.
        /// Accepts email or numeric user ID.
        /// </summary>
        [HttpPut("users/{userIdentifier}")]
        public async Task<IActionResult> UpdateUser(string userIdentifier, [FromBody] UpdateUserDTO dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var userId = await _resolver.ResolveUserIdAsync(userIdentifier);
                var user = await _adminService.UpdateUserAsync(userId, dto);
                return Ok(user);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Get user by email or numeric user ID.
        /// </summary>
        [HttpGet("users/{userIdentifier}")]
        public async Task<IActionResult> GetUserById(string userIdentifier)
        {
            try
            {
                var userId = await _resolver.ResolveUserIdAsync(userIdentifier);
                var user = await _adminService.GetUserByIdAsync(userId);
                if (user == null)
                    return NotFound(new { message = "User not found" });
                return Ok(user);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Get all users.
        /// </summary>
        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers()
        {
            try
            {
                var users = await _adminService.GetAllUsersAsync();
                return Ok(users);
            }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Get users by role (admin, lecturer, student).
        /// </summary>
        [HttpGet("users/role/{role}")]
        public async Task<IActionResult> GetUsersByRole(string role)
        {
            try
            {
                var users = await _adminService.GetUsersByRoleAsync(role);
                return Ok(users);
            }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Delete user. Accepts email or numeric user ID.
        /// </summary>
        [HttpDelete("users/{userIdentifier}")]
        public async Task<IActionResult> DeleteUser(string userIdentifier)
        {
            try
            {
                var userId = await _resolver.ResolveUserIdAsync(userIdentifier);
                await _adminService.DeleteUserAsync(userId);
                return Ok(new { message = "User deleted successfully" });
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Set user status (active/inactive). Accepts email or numeric user ID.
        /// </summary>
        [HttpPatch("users/{userIdentifier}/status")]
        public async Task<IActionResult> SetUserStatus(string userIdentifier, [FromBody] SetUserStatusDTO dto)
        {
            try
            {
                var userId = await _resolver.ResolveUserIdAsync(userIdentifier);
                await _adminService.SetUserStatusAsync(userId, dto.Status);
                return Ok(new { message = "User status updated successfully" });
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        #endregion

        #region Student Group Management

        /// <summary>
        /// Create a new student group.
        /// </summary>
        [HttpPost("groups")]
        public async Task<IActionResult> CreateStudentGroup([FromBody] CreateStudentGroupDTO dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var group = await _adminService.CreateStudentGroupAsync(dto);
                return CreatedAtAction(nameof(GetStudentGroupById), new { groupCode = group.GroupCode }, group);
            }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Update student group. Accepts group code (e.g. "SE1234") or numeric group ID.
        /// </summary>
        [HttpPut("groups/{groupCode}")]
        public async Task<IActionResult> UpdateStudentGroup(string groupCode, [FromBody] UpdateStudentGroupDTO dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var groupId = await _resolver.ResolveGroupIdAsync(groupCode);
                var group = await _adminService.UpdateStudentGroupAsync(groupId, dto);
                return Ok(group);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Get student group by group code (e.g. "SE1234") or numeric group ID.
        /// </summary>
        [HttpGet("groups/{groupCode}")]
        public async Task<IActionResult> GetStudentGroupById(string groupCode)
        {
            try
            {
                var groupId = await _resolver.ResolveGroupIdAsync(groupCode);
                var group = await _adminService.GetStudentGroupByIdAsync(groupId);
                if (group == null)
                    return NotFound(new { message = "Student group not found" });
                return Ok(group);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Get all student groups.
        /// </summary>
        [HttpGet("groups")]
        public async Task<IActionResult> GetAllStudentGroups()
        {
            try
            {
                var groups = await _adminService.GetAllStudentGroupsAsync();
                return Ok(groups);
            }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Delete student group. Accepts group code (e.g. "SE1234") or numeric group ID.
        /// </summary>
        [HttpDelete("groups/{groupCode}")]
        public async Task<IActionResult> DeleteStudentGroup(string groupCode)
        {
            try
            {
                var groupId = await _resolver.ResolveGroupIdAsync(groupCode);
                await _adminService.DeleteStudentGroupAsync(groupId);
                return Ok(new { message = "Student group deleted successfully (soft delete)" });
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        #endregion

        #region Lecturer Management

        /// <summary>
        /// Get all lecturers.
        /// </summary>
        [HttpGet("lecturers")]
        public async Task<IActionResult> GetAllLecturers()
        {
            try
            {
                var lecturers = await _adminService.GetAllLecturersAsync();
                return Ok(lecturers);
            }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Assign lecturer to group.
        /// Accepts group code (e.g. "SE1234") or numeric group ID.
        /// </summary>
        [HttpPut("groups/{groupCode}/lecturer")]
        public async Task<IActionResult> AssignLecturerToGroup(string groupCode, [FromBody] AssignLecturerDTO dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var groupId = await _resolver.ResolveGroupIdAsync(groupCode);
                await _adminService.AssignLecturerToGroupAsync(groupId, dto.LecturerId);
                return Ok(new { message = "Lecturer assigned to group successfully" });
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        #endregion

        #region Group Member Management

        /// <summary>
        /// Add student to group.
        /// Accepts group code (e.g. "SE1234") or numeric group ID, and student user ID.
        /// </summary>
        [HttpPost("groups/{groupCode}/members/{studentId}")]
        public async Task<IActionResult> AddStudentToGroup(string groupCode, int studentId)
        {
            try
            {
                var groupId = await _resolver.ResolveGroupIdAsync(groupCode);
                await _adminService.AddStudentToGroupAsync(groupId, studentId);
                return Ok(new { message = "Student added to group successfully" });
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Remove student from group.
        /// Accepts group code (e.g. "SE1234") or numeric group ID, and student user ID.
        /// </summary>
        [HttpDelete("groups/{groupCode}/members/{studentId}")]
        public async Task<IActionResult> RemoveStudentFromGroup(string groupCode, int studentId)
        {
            try
            {
                var groupId = await _resolver.ResolveGroupIdAsync(groupCode);
                await _adminService.RemoveStudentFromGroupAsync(groupId, studentId);
                return Ok(new { message = "Student removed from group successfully" });
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception ex)
            {
                var errorMessage = ex.Message;
                if (ex.InnerException != null)
                    errorMessage += " | Inner: " + ex.InnerException.Message;
                return BadRequest(new { message = errorMessage });
            }
        }

        /// <summary>
        /// Clear all members from a group. Only allowed when the group's project is marked as completed.
        /// Accepts group code (e.g. "SE1234") or numeric group ID.
        /// </summary>
        [HttpDelete("groups/{groupCode}/members")]
        public async Task<IActionResult> ClearGroupMembers(string groupCode)
        {
            try
            {
                var groupId = await _resolver.ResolveGroupIdAsync(groupCode);
                await _adminService.ClearGroupMembersOnCompletionAsync(groupId);
                return Ok(new { message = "All members removed from group successfully" });
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        #endregion

        #region User Integration Management (GitHub & Jira)

        /// <summary>
        /// Configure GitHub integration for a user — sets their github_username.
        /// Accepts email or numeric user ID.
        /// </summary>
        [HttpPost("users/{userIdentifier}/github")]
        [ProducesResponseType(typeof(UserResponseDTO), StatusCodes.Status200OK)]
        public async Task<IActionResult> ConfigureGithub(string userIdentifier, [FromBody] GitHubConfigureRequest request)
        {
            try
            {
                var targetUserId = await _resolver.ResolveUserIdAsync(userIdentifier);
                var user = await _integrationService.ConfigureGithubAsync(GetCurrentUserId(), targetUserId, request.GithubUsername);
                return Ok(user);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Remove GitHub integration for a user — clears their github_username.
        /// Accepts email or numeric user ID.
        /// </summary>
        [HttpDelete("users/{userIdentifier}/github")]
        [ProducesResponseType(typeof(UserResponseDTO), StatusCodes.Status200OK)]
        public async Task<IActionResult> RemoveGithub(string userIdentifier)
        {
            try
            {
                var targetUserId = await _resolver.ResolveUserIdAsync(userIdentifier);
                var user = await _integrationService.RemoveGithubAsync(GetCurrentUserId(), targetUserId);
                return Ok(user);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Configure Jira integration for a user — sets their jira_account_id.
        /// Accepts email or numeric user ID.
        /// </summary>
        [HttpPost("users/{userIdentifier}/jira")]
        [ProducesResponseType(typeof(UserResponseDTO), StatusCodes.Status200OK)]
        public async Task<IActionResult> ConfigureJira(string userIdentifier, [FromBody] JiraConfigureRequest request)
        {
            try
            {
                var targetUserId = await _resolver.ResolveUserIdAsync(userIdentifier);
                var user = await _integrationService.ConfigureJiraAsync(GetCurrentUserId(), targetUserId, request.JiraAccountId);
                return Ok(user);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Remove Jira integration for a user — clears their jira_account_id.
        /// Accepts email or numeric user ID.
        /// </summary>
        [HttpDelete("users/{userIdentifier}/jira")]
        [ProducesResponseType(typeof(UserResponseDTO), StatusCodes.Status200OK)]
        public async Task<IActionResult> RemoveJira(string userIdentifier)
        {
            try
            {
                var targetUserId = await _resolver.ResolveUserIdAsync(userIdentifier);
                var user = await _integrationService.RemoveJiraAsync(GetCurrentUserId(), targetUserId);
                return Ok(user);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
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

        #region Project Management

        /// <summary>
        /// Create a project for a group. Each group can only have one project.
        /// Accepts group code (e.g. "SE1234") or numeric group ID in the route.
        /// </summary>
        [HttpPost("groups/{groupCode}/project")]
        [ProducesResponseType(typeof(ProjectResponseDTO), StatusCodes.Status201Created)]
        public async Task<IActionResult> CreateProject(string groupCode, [FromBody] CreateProjectDTO dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var groupId = await _resolver.ResolveGroupIdAsync(groupCode);
                var project = await _adminService.CreateProjectAsync(groupId, dto);
                return CreatedAtAction(nameof(GetProjectByGroup), new { groupCode }, project);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Get the project for a specific group.
        /// Accepts group code (e.g. "SE1234") or numeric group ID.
        /// </summary>
        [HttpGet("groups/{groupCode}/project")]
        [ProducesResponseType(typeof(ProjectResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetProjectByGroup(string groupCode)
        {
            try
            {
                var groupId = await _resolver.ResolveGroupIdAsync(groupCode);
                var project = await _adminService.GetProjectByGroupIdAsync(groupId);
                if (project == null)
                    return NotFound(new { message = $"No project found for group '{groupCode}'" });
                return Ok(project);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Get all projects.
        /// </summary>
        [HttpGet("projects")]
        [ProducesResponseType(typeof(List<ProjectResponseDTO>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllProjects()
        {
            try
            {
                var projects = await _adminService.GetAllProjectsAsync();
                return Ok(projects);
            }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Update a project. Accepts group code (e.g. "SE1234") or numeric group ID to find the project.
        /// </summary>
        [HttpPut("groups/{groupCode}/project")]
        [ProducesResponseType(typeof(ProjectResponseDTO), StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdateProject(string groupCode, [FromBody] UpdateProjectDTO dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var groupId = await _resolver.ResolveGroupIdAsync(groupCode);
                var existing = await _adminService.GetProjectByGroupIdAsync(groupId);
                if (existing == null)
                    return NotFound(new { message = $"No project found for group '{groupCode}'" });

                var project = await _adminService.UpdateProjectAsync(existing.ProjectId, dto);
                return Ok(project);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Delete a project. Accepts group code (e.g. "SE1234") or numeric group ID.
        /// </summary>
        [HttpDelete("groups/{groupCode}/project")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> DeleteProject(string groupCode)
        {
            try
            {
                var groupId = await _resolver.ResolveGroupIdAsync(groupCode);
                var existing = await _adminService.GetProjectByGroupIdAsync(groupId);
                if (existing == null)
                    return NotFound(new { message = $"No project found for group '{groupCode}'" });

                await _adminService.DeleteProjectAsync(existing.ProjectId);
                return Ok(new { message = "Project deleted successfully" });
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        #endregion
    }
}
