using BLL.DTOs.Jira;
using BLL.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace SWP391_JGMS.Controllers
{
    /// <summary>
    /// Jira Integration Controller
    /// Manages Jira configuration, synchronization, and issue viewing
    /// </summary>
    [ApiController]
    [Route("api/jira")]
    [Authorize]
    public class JiraController : ControllerBase
    {
        private readonly IJiraIntegrationService _jiraService;

        public JiraController(IJiraIntegrationService jiraService)
        {
            _jiraService = jiraService;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.Parse(userIdClaim ?? "0");
        }

        // ============================================================================
        // Admin: Configuration Management
        // ============================================================================

        /// <summary>
        /// Configure Jira integration for a project (Admin only)
        /// </summary>
        /// <remarks>
        /// Sample request:
        ///
        ///     POST /api/jira/projects/1/integration
        ///     {
        ///         "jiraUrl": "https://yourcompany.atlassian.net",
        ///         "jiraEmail": "admin@fpt.edu.vn",
        ///         "apiToken": "ATATT3xFfGN0...",
        ///         "projectKey": "SWP391"
        ///     }
        ///
        /// </remarks>
        [HttpPost("projects/{projectId}/integration")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<JiraIntegrationResponseDTO>> ConfigureIntegration(
            int projectId,
            [FromBody] ConfigureJiraIntegrationDTO dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _jiraService.ConfigureIntegrationAsync(userId, projectId, dto);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Get Jira integration configuration for a project
        /// </summary>
        [HttpGet("projects/{projectId}/integration")]
        public async Task<ActionResult<JiraIntegrationResponseDTO>> GetIntegration(int projectId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _jiraService.GetIntegrationAsync(userId, projectId);

                if (result == null)
                {
                    return NotFound(new { message = "Jira integration not configured for this project" });
                }

                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Update Jira integration configuration (Admin only)
        /// </summary>
        [HttpPut("projects/{projectId}/integration")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<JiraIntegrationResponseDTO>> UpdateIntegration(
            int projectId,
            [FromBody] ConfigureJiraIntegrationDTO dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _jiraService.UpdateIntegrationAsync(userId, projectId, dto);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Delete Jira integration configuration (Admin only)
        /// </summary>
        [HttpDelete("projects/{projectId}/integration")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult> DeleteIntegration(int projectId)
        {
            try
            {
                var userId = GetCurrentUserId();
                await _jiraService.DeleteIntegrationAsync(userId, projectId);
                return Ok(new { message = "Jira integration deleted successfully" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Test Jira connection (Admin only)
        /// </summary>
        [HttpGet("projects/{projectId}/integration/test")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<JiraConnectionTestDTO>> TestConnection(int projectId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _jiraService.TestConnectionAsync(userId, projectId);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Get all Jira integrations (Admin only)
        /// </summary>
        [HttpGet("integrations")]
        [Authorize(Roles = "admin")]
        public async Task<ActionResult<List<JiraIntegrationResponseDTO>>> GetAllIntegrations()
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _jiraService.GetAllIntegrationsAsync(userId);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // ============================================================================
        // Synchronization
        // ============================================================================

        /// <summary>
        /// Sync issues from Jira to database (Admin or Team Leader)
        /// </summary>
        /// <remarks>
        /// This endpoint synchronizes all issues from the configured Jira project
        /// to your local database. New issues are created, existing issues are updated.
        ///
        /// Role permissions:
        /// - Admin: Can sync any project
        /// - Team Leader: Can sync their own project
        /// </remarks>
        [HttpPost("projects/{projectId}/sync")]
        [Authorize(Roles = "admin,student")]
        public async Task<ActionResult<JiraSyncResultDTO>> SyncIssues(int projectId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _jiraService.SyncIssuesAsync(userId, projectId);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Get sync status for a project
        /// </summary>
        [HttpGet("projects/{projectId}/sync-status")]
        public async Task<ActionResult<JiraSyncResultDTO>> GetSyncStatus(int projectId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _jiraService.GetSyncStatusAsync(userId, projectId);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // ============================================================================
        // Issue Viewing (Role-based)
        // ============================================================================

        /// <summary>
        /// Get all synced Jira issues for a project (role-based filtering)
        /// </summary>
        /// <remarks>
        /// Returns issues based on user role:
        /// - Admin: All issues
        /// - Lecturer: All issues in their assigned groups (read-only)
        /// - Team Leader: All issues in their project
        /// - Student: Only issues assigned to them
        /// </remarks>
        [HttpGet("projects/{projectId}/issues")]
        public async Task<ActionResult<List<JiraIssueDTO>>> GetProjectIssues(int projectId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _jiraService.GetProjectIssuesAsync(userId, projectId);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Get single issue details by issue key (e.g., "SWP391-123")
        /// </summary>
        [HttpGet("issues/{issueKey}")]
        public async Task<ActionResult<JiraIssueDTO>> GetIssueDetails(string issueKey)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _jiraService.GetIssueDetailsAsync(userId, issueKey);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}

