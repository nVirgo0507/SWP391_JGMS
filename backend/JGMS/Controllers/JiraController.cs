using BLL.DTOs.Jira;
using BLL.Helpers;
using BLL.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace SWP391_JGMS.Controllers
{
    /// <summary>
    /// Jira Integration Controller.
    /// Manages Jira configuration, synchronization, and issue viewing.
    ///
    /// All endpoints accept group code (e.g. "SE1234") or numeric group ID
    /// to identify the project — no need to know the internal project ID.
    /// </summary>
    [ApiController]
    [Route("api/jira")]
    [Authorize]
    public class JiraController : ControllerBase
    {
        private readonly IJiraIntegrationService _jiraService;
        private readonly IdentifierResolver _resolver;

        public JiraController(IJiraIntegrationService jiraService, IdentifierResolver resolver)
        {
            _jiraService = jiraService;
            _resolver = resolver;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.Parse(userIdClaim ?? "0");
        }

        // ============================================================================
        // Admin: Configuration Management
        // All accept group code (e.g. "SE1234") or numeric group ID to identify project.
        // ============================================================================

        /// <summary>
        /// Configure Jira integration for a project (Admin only).
        /// Accepts group code (e.g. "SE1234") or numeric group ID.
        /// </summary>
        /// <remarks>
        /// Sample request:
        ///
        ///     POST /api/jira/groups/SE1234/integration
        ///     {
        ///         "jiraUrl": "https://yourcompany.atlassian.net",
        ///         "jiraEmail": "admin@fpt.edu.vn",
        ///         "apiToken": "ATATT3xFfGN0...",
        ///         "projectKey": "SWP391"
        ///     }
        ///
        /// </remarks>
        [HttpPost("groups/{groupCode}/integration")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<JiraIntegrationResponseDTO>> ConfigureIntegration(
            string groupCode,
            [FromBody] ConfigureJiraIntegrationDTO dto)
        {
            try
            {
                var projectId = await _resolver.ResolveProjectIdAsync(groupCode);
                var result = await _jiraService.ConfigureIntegrationAsync(GetCurrentUserId(), projectId, dto);
                return Ok(result);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Get Jira integration configuration for a project.
        /// Accepts group code (e.g. "SE1234") or numeric group ID.
        /// </summary>
        [HttpGet("groups/{groupCode}/integration")]
        public async Task<ActionResult<JiraIntegrationResponseDTO>> GetIntegration(string groupCode)
        {
            try
            {
                var projectId = await _resolver.ResolveProjectIdAsync(groupCode);
                var result = await _jiraService.GetIntegrationAsync(GetCurrentUserId(), projectId);
                if (result == null)
                    return NotFound(new { message = "Jira integration not configured for this project" });
                return Ok(result);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Update Jira integration configuration (Admin only).
        /// Accepts group code (e.g. "SE1234") or numeric group ID.
        /// </summary>
        [HttpPut("groups/{groupCode}/integration")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<JiraIntegrationResponseDTO>> UpdateIntegration(
            string groupCode,
            [FromBody] ConfigureJiraIntegrationDTO dto)
        {
            try
            {
                var projectId = await _resolver.ResolveProjectIdAsync(groupCode);
                var result = await _jiraService.UpdateIntegrationAsync(GetCurrentUserId(), projectId, dto);
                return Ok(result);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Delete Jira integration configuration (Admin only).
        /// Accepts group code (e.g. "SE1234") or numeric group ID.
        /// </summary>
        [HttpDelete("groups/{groupCode}/integration")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult> DeleteIntegration(string groupCode)
        {
            try
            {
                var projectId = await _resolver.ResolveProjectIdAsync(groupCode);
                await _jiraService.DeleteIntegrationAsync(GetCurrentUserId(), projectId);
                return Ok(new { message = "Jira integration deleted successfully" });
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Test Jira connection (Admin only).
        /// Accepts group code (e.g. "SE1234") or numeric group ID.
        /// </summary>
        [HttpGet("groups/{groupCode}/integration/test")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<JiraConnectionTestDTO>> TestConnection(string groupCode)
        {
            try
            {
                var projectId = await _resolver.ResolveProjectIdAsync(groupCode);
                var result = await _jiraService.TestConnectionAsync(GetCurrentUserId(), projectId);
                return Ok(result);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Get all Jira integrations (Admin only).
        /// </summary>
        [HttpGet("integrations")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<List<JiraIntegrationResponseDTO>>> GetAllIntegrations()
        {
            try
            {
                var result = await _jiraService.GetAllIntegrationsAsync(GetCurrentUserId());
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        // ============================================================================
        // Synchronization
        // ============================================================================

        /// <summary>
        /// Sync issues from Jira to database (Admin or Team Leader).
        /// Accepts group code (e.g. "SE1234") or numeric group ID.
        /// </summary>
        [HttpPost("groups/{groupCode}/sync")]
        [Authorize(Roles = "admin,student")]
        public async Task<ActionResult<JiraSyncResultDTO>> SyncIssues(string groupCode)
        {
            try
            {
                var groupId = await _resolver.ResolveGroupIdAsync(groupCode);
                var result = await _jiraService.SyncIssuesByGroupAsync(GetCurrentUserId(), groupId);
                return Ok(result);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Get sync status for a group's project.
        /// Accepts group code (e.g. "SE1234") or numeric group ID.
        /// </summary>
        [HttpGet("groups/{groupCode}/sync-status")]
        public async Task<ActionResult<JiraSyncResultDTO>> GetSyncStatus(string groupCode)
        {
            try
            {
                var groupId = await _resolver.ResolveGroupIdAsync(groupCode);
                var result = await _jiraService.GetSyncStatusByGroupAsync(GetCurrentUserId(), groupId);
                return Ok(result);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        // ============================================================================
        // Issue Viewing
        // ============================================================================

        /// <summary>
        /// Get all synced Jira issues for a group's project (role-based filtering).
        /// Accepts group code (e.g. "SE1234") or numeric group ID.
        /// </summary>
        [HttpGet("groups/{groupCode}/issues")]
        public async Task<ActionResult<List<JiraIssueDTO>>> GetProjectIssues(string groupCode)
        {
            try
            {
                var groupId = await _resolver.ResolveGroupIdAsync(groupCode);
                var result = await _jiraService.GetProjectIssuesByGroupAsync(GetCurrentUserId(), groupId);
                return Ok(result);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Get single issue details by issue key (e.g. "SWP391-123").
        /// </summary>
        [HttpGet("issues/{issueKey}")]
        public async Task<ActionResult<JiraIssueDTO>> GetIssueDetails(string issueKey)
        {
            try
            {
                var result = await _jiraService.GetIssueDetailsAsync(GetCurrentUserId(), issueKey);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }
    }
}

