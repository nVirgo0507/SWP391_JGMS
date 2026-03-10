using BLL.DTOs.Github;
using BLL.Services.Interface;
using DAL.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace JGMS.Controllers
{
    [Route("api/github")]
    [ApiController]
    [Authorize]
    public class GithubController : ControllerBase
    {
        private readonly IGithubApiService _githubApiService;
        private readonly IGithubIntegrationService _githubIntegrationService;

        public GithubController(IGithubApiService githubApiService, IGithubIntegrationService githubIntegrationService)
        {
            _githubApiService = githubApiService;
            _githubIntegrationService = githubIntegrationService;
        }

        [HttpGet("{projectId}/branches")]
        public async Task<ActionResult<List<GithubBranchDto>>> GetBranches(int projectId)
        {
            try
            {
                var branches = await _githubApiService.GetBranchesAsync(projectId);
                return Ok(branches);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpGet("{projectId}/pull-requests")]
        public async Task<ActionResult<List<GithubPullRequestDto>>> GetPullRequests(int projectId)
        {
            try
            {
                var prs = await _githubApiService.GetPullRequestsAsync(projectId);
                return Ok(prs);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpGet("{projectId}/commits")]
        public async Task<ActionResult> GetCommits(int projectId)
        {
            try
            {
                var commits = await _githubApiService.GetCommitsAsync(projectId);
                return Ok(commits);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpPost("{projectId}/sync")]
        public async Task<ActionResult> SyncCommits(int projectId)
        {
            try
            {
                await _githubIntegrationService.SyncCommitsAsync(projectId);
                return Ok(new { Message = "Commits synced successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpPost("webhook")]
        [AllowAnonymous]
        public async Task<ActionResult> ReceiveWebhook()
        {
            try
            {
                var eventType = Request.Headers["X-GitHub-Event"].ToString();
                using var reader = new StreamReader(Request.Body);
                var payload = await reader.ReadToEndAsync();

                await _githubIntegrationService.ProcessWebhookEventAsync(eventType, payload);

                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = "Error processing webhook." });
            }
        }
    }
}
