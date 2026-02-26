﻿using BLL.DTOs.Jira;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace BLL.Services.Interface
{
    /// <summary>
    /// Service for calling Jira REST API v3
    /// Uses HttpClient for direct API calls
    /// </summary>
    public interface IJiraApiService
    {
        /// <summary>
        /// Test connection to Jira instance
        /// </summary>
        Task<bool> TestConnectionAsync(string jiraUrl, string email, string apiToken);

        /// <summary>
        /// Get Jira project information
        /// </summary>
        Task<JiraProjectDTO> GetProjectAsync(string jiraUrl, string email, string apiToken, string projectKey);

        /// <summary>
        /// Get all issues for a project using JQL
        /// </summary>
        Task<List<JiraIssueDTO>> GetProjectIssuesAsync(string jiraUrl, string email, string apiToken, string projectKey);

        /// <summary>
        /// Get single issue by key (e.g., "SWP391-123")
        /// </summary>
        Task<JiraIssueDTO> GetIssueAsync(string jiraUrl, string email, string apiToken, string issueKey);

        /// <summary>
        /// Create new issue in Jira
        /// </summary>
        Task<JiraIssueDTO> CreateIssueAsync(string jiraUrl, string email, string apiToken, CreateJiraIssueDTO dto);

        /// <summary>
        /// Update existing issue
        /// </summary>
        Task<JiraIssueDTO> UpdateIssueAsync(string jiraUrl, string email, string apiToken, string issueKey, UpdateJiraIssueDTO dto);

        /// <summary>
        /// Get available transitions for an issue (workflow states)
        /// </summary>
        Task<List<JiraTransitionDTO>> GetAvailableTransitionsAsync(string jiraUrl, string email, string apiToken, string issueKey);

        /// <summary>
        /// Transition issue to new status
        /// </summary>
        Task TransitionIssueAsync(string jiraUrl, string email, string apiToken, string issueKey, string transitionId);

        /// <summary>
        /// Search for user by email or account ID
        /// </summary>
        Task<string?> SearchUserAsync(string jiraUrl, string email, string apiToken, string searchTerm);

        /// <summary>
        /// Move a Jira issue into a sprint (Jira Software Agile API)
        /// </summary>
        Task MoveIssueToSprintAsync(string jiraUrl, string email, string apiToken, string issueKey, int sprintId);

        /// <summary>
        /// Remove a Jira issue from its current sprint (move to backlog)
        /// </summary>
        Task MoveIssueToBacklogAsync(string jiraUrl, string email, string apiToken, string issueKey);

        /// <summary>
        /// Create a Jira issue link (e.g. "is related to", "blocks")
        /// </summary>
        Task CreateIssueLinkAsync(string jiraUrl, string email, string apiToken, string fromIssueKey, string toIssueKey, string linkType = "Relates");

        /// <summary>
        /// Delete a Jira issue by key
        /// </summary>
        Task DeleteIssueAsync(string jiraUrl, string email, string apiToken, string issueKey);
    }
}

