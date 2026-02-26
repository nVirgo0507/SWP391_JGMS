using BLL.DTOs.Admin;
using BLL.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Security.Claims;

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
    [Authorize]
    public class TeamLeaderController : ControllerBase
    {
        private readonly ITeamLeaderService _teamLeaderService;

        public TeamLeaderController(ITeamLeaderService teamLeaderService)
        {
            _teamLeaderService = teamLeaderService;
        }

        /// <summary>Reads the authenticated user's ID from the JWT sub claim.</summary>
        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (int.TryParse(userIdClaim, out var id)) return id;
            throw new UnauthorizedAccessException("Invalid or missing user identity in token.");
        }

        #region Project Management

        /// <summary>
        /// BR-055: Get project details for the leader's group.
        /// userId is read from the JWT — do NOT pass it in the query string.
        /// </summary>
        [HttpGet("groups/{groupId}/project")]
        [ProducesResponseType(typeof(ProjectResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetGroupProject(int groupId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var project = await _teamLeaderService.GetGroupProjectAsync(userId, groupId);
                if (project == null)
                    return NotFound(new { message = "Project not found" });
                return Ok(project);
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Access denied")) return Forbid();
                return BadRequest(new { message = ex.Message });
            }
        }

        #endregion

        #region Requirements Management

        /// <summary>
        /// Get all requirements for the leader's group project.
        /// Requirements are linked to Jira issues and show type (Epic/Story/Task), priority and status.
        /// </summary>
        [HttpGet("groups/{groupId}/requirements")]
        [ProducesResponseType(typeof(List<RequirementResponseDTO>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetGroupRequirements(int groupId)
        {
            try
            {
                var requirements = await _teamLeaderService.GetGroupRequirementsAsync(GetCurrentUserId(), groupId);
                return Ok(requirements);
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return ex.Message.Contains("Access denied") ? Forbid() : BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Create a requirement and push it to Jira as a new issue.
        /// Set IssueType (Epic | Story | Task | Sub-task | Bug) and Priority (Highest | High | Medium | Low | Lowest).
        /// If Jira integration is not configured, the requirement is saved locally only.
        /// </summary>
        [HttpPost("groups/{groupId}/requirements")]
        [ProducesResponseType(typeof(RequirementResponseDTO), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateRequirement(int groupId, [FromBody] CreateRequirementDTO dto)
        {
            try
            {
                var requirement = await _teamLeaderService.CreateRequirementAsync(GetCurrentUserId(), groupId, dto);
                return CreatedAtAction(nameof(GetGroupRequirements), new { groupId }, requirement);
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return ex.Message.Contains("Access denied") ? Forbid() : BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Update a requirement's fields and sync the changes back to the linked Jira issue.
        /// </summary>
        [HttpPut("groups/{groupId}/requirements/{requirementId}")]
        [ProducesResponseType(typeof(RequirementResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateRequirement(int groupId, int requirementId, [FromBody] UpdateRequirementDTO dto)
        {
            try
            {
                var requirement = await _teamLeaderService.UpdateRequirementAsync(GetCurrentUserId(), groupId, requirementId, dto);
                return Ok(requirement);
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return ex.Message.Contains("Access denied") ? Forbid() : BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Delete a requirement and remove the corresponding issue from Jira.
        /// </summary>
        [HttpDelete("groups/{groupId}/requirements/{requirementId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DeleteRequirement(int groupId, int requirementId)
        {
            try
            {
                await _teamLeaderService.DeleteRequirementAsync(GetCurrentUserId(), groupId, requirementId);
                return Ok(new { message = "Requirement deleted successfully" });
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return ex.Message.Contains("Access denied") ? Forbid() : BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Organise requirements hierarchy (Epic → Story → Task → Sub-task).
        /// Pass a list of requirement IDs with their desired display order.
        /// Returns the requirements sorted in the requested order.
        /// </summary>
        [HttpPut("groups/{groupId}/requirements/reorder")]
        [ProducesResponseType(typeof(List<RequirementResponseDTO>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> ReorderRequirements(int groupId, [FromBody] ReorderRequirementsDTO dto)
        {
            try
            {
                var ordered = await _teamLeaderService.ReorderRequirementsAsync(GetCurrentUserId(), groupId, dto);
                return Ok(ordered);
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return ex.Message.Contains("Access denied") ? Forbid() : BadRequest(new { message = ex.Message }); }
        }

        #endregion

        #region Tasks Management

        /// <summary>Get all tasks for the leader's group project.</summary>
        [HttpGet("groups/{groupId}/tasks")]
        [ProducesResponseType(typeof(List<TaskResponseDTO>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetGroupTasks(int groupId)
        {
            try
            {
                var tasks = await _teamLeaderService.GetGroupTasksAsync(GetCurrentUserId(), groupId);
                return Ok(tasks);
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return ex.Message.Contains("Access denied") ? Forbid() : BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Create a task, assign a member, set deadline and optionally add to a Jira sprint.
        /// If jiraIssueId is provided the task is linked to that Jira issue and changes are synced.
        /// If sprintId is also provided the linked Jira issue is moved into that sprint.
        /// </summary>
        [HttpPost("groups/{groupId}/tasks")]
        [ProducesResponseType(typeof(TaskResponseDTO), StatusCodes.Status201Created)]
        public async Task<IActionResult> CreateTask(int groupId, [FromBody] CreateTaskDTO dto)
        {
            try
            {
                var task = await _teamLeaderService.CreateTaskAsync(GetCurrentUserId(), groupId, dto);
                return CreatedAtAction(nameof(GetGroupTasks), new { groupId }, task);
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return ex.Message.Contains("Access denied") ? Forbid() : BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Create a task pre-populated from a synced Jira issue key (e.g. "SWP391-5").
        /// Title, description, and priority are auto-filled from the Jira issue.
        /// </summary>
        [HttpPost("groups/{groupId}/tasks/from-jira")]
        [ProducesResponseType(typeof(TaskResponseDTO), StatusCodes.Status201Created)]
        public async Task<IActionResult> CreateTaskFromJiraIssue(int groupId, [FromBody] CreateTaskFromJiraIssueDTO dto)
        {
            try
            {
                var task = await _teamLeaderService.CreateTaskFromJiraIssueAsync(GetCurrentUserId(), groupId, dto);
                return CreatedAtAction(nameof(GetGroupTasks), new { groupId }, task);
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return ex.Message.Contains("Access denied") ? Forbid() : BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Edit a task: update status, assignee, description, priority, due date, or linked requirement.
        /// Changes are synced back to Jira if the task is linked to a Jira issue.
        /// Status changes trigger a Jira workflow transition automatically.
        /// </summary>
        [HttpPut("groups/{groupId}/tasks/{taskId}")]
        [ProducesResponseType(typeof(TaskResponseDTO), StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdateTask(int groupId, int taskId, [FromBody] UpdateTaskDTO dto)
        {
            try
            {
                var task = await _teamLeaderService.UpdateTaskAsync(GetCurrentUserId(), groupId, taskId, dto);
                return Ok(task);
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return ex.Message.Contains("Access denied") ? Forbid() : BadRequest(new { message = ex.Message }); }
        }

        /// <summary>Assign a task to a group member.</summary>
        [HttpPost("groups/{groupId}/tasks/{taskId}/assign")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> AssignTask(int groupId, int taskId, [FromBody] AssignTaskDTO dto)
        {
            try
            {
                await _teamLeaderService.AssignTaskAsync(GetCurrentUserId(), groupId, taskId, dto.MemberId);
                return Ok(new { message = "Task assigned successfully" });
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return ex.Message.Contains("Access denied") ? Forbid() : BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Delete a task. Also removes the linked Jira issue from Jira if integration is configured.
        /// </summary>
        [HttpDelete("groups/{groupId}/tasks/{taskId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> DeleteTask(int groupId, int taskId)
        {
            try
            {
                await _teamLeaderService.DeleteTaskAsync(GetCurrentUserId(), groupId, taskId);
                return Ok(new { message = "Task deleted successfully" });
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return ex.Message.Contains("Access denied") ? Forbid() : BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Move a task's linked Jira issue to a different sprint.
        /// Use sprintId = 0 to move the issue back to the backlog.
        /// Requires the task to be linked to a Jira issue and Jira integration to be configured.
        /// </summary>
        [HttpPut("groups/{groupId}/tasks/{taskId}/sprint")]
        [ProducesResponseType(typeof(TaskResponseDTO), StatusCodes.Status200OK)]
        public async Task<IActionResult> MoveTaskToSprint(int groupId, int taskId, [FromBody] MoveTaskToSprintDTO dto)
        {
            try
            {
                var task = await _teamLeaderService.MoveTaskToSprintAsync(GetCurrentUserId(), groupId, taskId, dto.SprintId);
                return Ok(task);
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return ex.Message.Contains("Access denied") ? Forbid() : BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Link a task to a requirement. Creates a "Relates" issue link in Jira if both sides
        /// have linked Jira issues and integration is configured.
        /// </summary>
        [HttpPut("groups/{groupId}/tasks/{taskId}/link-requirement")]
        [ProducesResponseType(typeof(TaskResponseDTO), StatusCodes.Status200OK)]
        public async Task<IActionResult> LinkTaskToRequirement(int groupId, int taskId, [FromBody] LinkTaskToRequirementDTO dto)
        {
            try
            {
                var task = await _teamLeaderService.LinkTaskToRequirementAsync(GetCurrentUserId(), groupId, taskId, dto.RequirementId);
                return Ok(task);
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return ex.Message.Contains("Access denied") ? Forbid() : BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Push all local changes (tasks + requirements) to Jira in bulk.
        /// Syncs title, description, status, priority, and assignee for every
        /// item that has a linked Jira issue.
        /// Returns a summary: how many synced, how many failed, errors and warnings.
        /// </summary>
        [HttpPost("groups/{groupId}/sync-to-jira")]
        [ProducesResponseType(typeof(BLL.DTOs.Jira.JiraPushSyncResultDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> SyncToJira(int groupId)
        {
            try
            {
                var result = await _teamLeaderService.SyncToJiraAsync(GetCurrentUserId(), groupId);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return ex.Message.Contains("Access denied") ? Forbid() : BadRequest(new { message = ex.Message }); }
        }

        #endregion

        #region SRS Document Management

        [HttpGet("groups/{groupId}/srs")]
        [ProducesResponseType(typeof(SrsDocumentResponseDTO), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetGroupSrsDocument(int groupId)
        {
            try
            {
                var srs = await _teamLeaderService.GetGroupSrsDocumentAsync(GetCurrentUserId(), groupId);
                if (srs == null)
                    return NotFound(new { message = "SRS document not found" });
                return Ok(srs);
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return ex.Message.Contains("Access denied") ? Forbid() : BadRequest(new { message = ex.Message }); }
        }

        [HttpPost("groups/{groupId}/srs")]
        [ProducesResponseType(typeof(SrsDocumentResponseDTO), StatusCodes.Status201Created)]
        public async Task<IActionResult> CreateSrsDocument(int groupId, [FromBody] CreateSrsDocumentDTO dto)
        {
            try
            {
                var srs = await _teamLeaderService.CreateSrsDocumentAsync(GetCurrentUserId(), groupId, dto);
                return CreatedAtAction(nameof(GetGroupSrsDocument), srs);
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return ex.Message.Contains("Access denied") ? Forbid() : BadRequest(new { message = ex.Message }); }
        }

        [HttpPut("groups/{groupId}/srs/{srsId}")]
        [ProducesResponseType(typeof(SrsDocumentResponseDTO), StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdateSrsDocument(int groupId, int srsId, [FromBody] UpdateSrsDocumentDTO dto)
        {
            try
            {
                var srs = await _teamLeaderService.UpdateSrsDocumentAsync(GetCurrentUserId(), groupId, srsId, dto);
                return Ok(srs);
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return ex.Message.Contains("Access denied") ? Forbid() : BadRequest(new { message = ex.Message }); }
        }

        #endregion
    }
}
