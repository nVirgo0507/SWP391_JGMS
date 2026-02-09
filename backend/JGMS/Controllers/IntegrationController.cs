using BLL.DTOs.Admin;
using BLL.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace SWP391_JGMS.Controllers
{
    /// <summary>
    /// Integration API endpoints with admin-only access control
    /// BR-058: Only Admin Configures Integrations - Only admin users can configure Jira and GitHub integrations
    /// Validation: Check user role = 'admin'
    /// Error Message: "Only administrators can configure integrations"
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class IntegrationController : ControllerBase
    {
        private readonly IIntegrationService _integrationService;

        public IntegrationController(IIntegrationService integrationService)
        {
            _integrationService = integrationService;
        }

        #region GitHub Integration

        /// <summary>
        /// BR-058: Configure GitHub integration for a user
        /// Only admin users can configure integrations
        /// </summary>
        /// <param name="adminUserId">The ID of the admin user performing the configuration</param>
        /// <param name="targetUserId">The ID of the user to configure</param>
        /// <param name="githubUsername">The GitHub username to configure</param>
        /// <returns>Updated user with GitHub integration</returns>
        [HttpPost("github/configure")]
        [ProducesResponseType(typeof(UserResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ConfigureGithub([FromQuery] int adminUserId, [FromQuery] int targetUserId, [FromBody] GitHubConfigureRequest request)
        {
            try
            {
                var user = await _integrationService.ConfigureGithubAsync(adminUserId, targetUserId, request.GithubUsername);
                return Ok(user);
            }
            catch (Exception ex)
            {
                // BR-058: Access denied if not admin
                if (ex.Message.Contains("Only administrators"))
                    return Forbid();
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// BR-058: Remove GitHub integration for a user
        /// Only admin users can remove integrations
        /// </summary>
        /// <param name="adminUserId">The ID of the admin user performing the removal</param>
        /// <param name="targetUserId">The ID of the user to remove integration from</param>
        /// <returns>Updated user with GitHub integration removed</returns>
        [HttpDelete("github/{targetUserId}")]
        [ProducesResponseType(typeof(UserResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RemoveGithub([FromQuery] int adminUserId, int targetUserId)
        {
            try
            {
                var user = await _integrationService.RemoveGithubAsync(adminUserId, targetUserId);
                return Ok(user);
            }
            catch (Exception ex)
            {
                // BR-058: Access denied if not admin
                if (ex.Message.Contains("Only administrators"))
                    return Forbid();
                return BadRequest(new { message = ex.Message });
            }
        }

        #endregion

        #region Jira Integration

        /// <summary>
        /// BR-058: Configure Jira integration for a user
        /// Only admin users can configure integrations
        /// </summary>
        /// <param name="adminUserId">The ID of the admin user performing the configuration</param>
        /// <param name="targetUserId">The ID of the user to configure</param>
        /// <param name="jiraAccountId">The Jira account ID to configure</param>
        /// <returns>Updated user with Jira integration</returns>
        [HttpPost("jira/configure")]
        [ProducesResponseType(typeof(UserResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ConfigureJira([FromQuery] int adminUserId, [FromQuery] int targetUserId, [FromBody] JiraConfigureRequest request)
        {
            try
            {
                var user = await _integrationService.ConfigureJiraAsync(adminUserId, targetUserId, request.JiraAccountId);
                return Ok(user);
            }
            catch (Exception ex)
            {
                // BR-058: Access denied if not admin
                if (ex.Message.Contains("Only administrators"))
                    return Forbid();
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// BR-058: Remove Jira integration for a user
        /// Only admin users can remove integrations
        /// </summary>
        /// <param name="adminUserId">The ID of the admin user performing the removal</param>
        /// <param name="targetUserId">The ID of the user to remove integration from</param>
        /// <returns>Updated user with Jira integration removed</returns>
        [HttpDelete("jira/{targetUserId}")]
        [ProducesResponseType(typeof(UserResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RemoveJira([FromQuery] int adminUserId, int targetUserId)
        {
            try
            {
                var user = await _integrationService.RemoveJiraAsync(adminUserId, targetUserId);
                return Ok(user);
            }
            catch (Exception ex)
            {
                // BR-058: Access denied if not admin
                if (ex.Message.Contains("Only administrators"))
                    return Forbid();
                return BadRequest(new { message = ex.Message });
            }
        }

        #endregion

        #region Integration Management

        /// <summary>
        /// BR-058: Get all configured integrations
        /// Only admin users can view all integrations
        /// </summary>
        /// <param name="adminUserId">The ID of the admin user</param>
        /// <returns>List of all users with configured integrations</returns>
        [HttpGet("all")]
        [ProducesResponseType(typeof(List<IntegrationStatusDTO>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetAllIntegrations([FromQuery] int adminUserId)
        {
            try
            {
                var integrations = await _integrationService.GetAllIntegrationsAsync(adminUserId);
                return Ok(integrations);
            }
            catch (Exception ex)
            {
                // BR-058: Access denied if not admin
                if (ex.Message.Contains("Only administrators"))
                    return Forbid();
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// BR-058: Test integration connectivity
        /// Only admin users can test integrations
        /// </summary>
        /// <param name="adminUserId">The ID of the admin user</param>
        /// <param name="integrationType">The type of integration to test (GitHub or Jira)</param>
        /// <returns>Test result showing connectivity status</returns>
        [HttpPost("test")]
        [ProducesResponseType(typeof(IntegrationTestResultDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> TestIntegration([FromQuery] int adminUserId, [FromQuery] string integrationType)
        {
            try
            {
                var result = await _integrationService.TestIntegrationAsync(adminUserId, integrationType);
                return Ok(result);
            }
            catch (Exception ex)
            {
                // BR-058: Access denied if not admin
                if (ex.Message.Contains("Only administrators"))
                    return Forbid();
                return BadRequest(new { message = ex.Message });
            }
        }

        #endregion
    }

    /// <summary>
    /// BR-058: Request DTO to configure GitHub integration
    /// </summary>
    public class GitHubConfigureRequest
    {
        public string GithubUsername { get; set; }
    }

    /// <summary>
    /// BR-058: Request DTO to configure Jira integration
    /// </summary>
    public class JiraConfigureRequest
    {
        public string JiraAccountId { get; set; }
    }
}
