﻿using BLL.DTOs.Student;
using BLL.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using System.IO;
using System.Security.Claims;

namespace SWP391_JGMS.Controllers
{
    /// <summary>
    /// Student API for basic operations:
    /// - View assigned tasks
    /// - Update task status
    /// - View personal statistics
    /// - Update profile information
    /// - Access SRS documents
    /// </summary>
    [ApiController]
    [Authorize(Roles = "student")]
	[Route("api/students")]
    [Produces("application/json")]
    public class StudentController : ControllerBase
    {
        private readonly IStudentService _studentService;

        public StudentController(IStudentService studentService)
        {
            _studentService = studentService;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (int.TryParse(userIdClaim, out var id)) return id;
            throw new UnauthorizedAccessException("Invalid or missing user identity in token.");
        }

        #region View Assigned Tasks

        /// <summary>
        /// Get all tasks assigned to the current student
        /// </summary>
        [HttpGet("tasks")]
        public async Task<IActionResult> GetMyTasks()
        {
            try
            {
                var tasks = await _studentService.GetMyTasksAsync(GetCurrentUserId());
                return Ok(tasks);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Get a specific task by ID
        /// </summary>
        [HttpGet("tasks/{taskId}")]
        public async Task<IActionResult> GetTaskById(int taskId)
        {
            try
            {
                var task = await _studentService.GetTaskByIdAsync(taskId, GetCurrentUserId());

                if (task == null)
                {
                    return NotFound(new { message = "Task not found" });
                }

                return Ok(task);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        #endregion

        #region Update Task Status

        /// <summary>
        /// Update the status of a task assigned to the student.
        /// Team members can change status (To Do → In Progress → Done)
        /// and add comments or log work hours.
        ///
        /// Accepted status values (case-insensitive, flexible formatting):
        /// - "todo", "To Do", "to do", "to_do"
        /// - "in_progress", "In Progress", "in progress", "in-progress"
        /// - "done", "Done"
        ///
        /// Spaces and hyphens are automatically normalized to underscores.
        /// </summary>
        /// <param name="taskId">Task ID</param>
        /// <param name="userId">Current user ID (will come from JWT claims in production)</param>
        /// <param name="dto">Update task status DTO with status, optional comment, and work hours</param>
        /// <response code="200">Status updated successfully</response>
        /// <response code="400">Invalid status value or task not found</response>
        /// <response code="403">User does not have permission to update this task</response>
        [HttpPut("tasks/{taskId}/status")]
        public async Task<IActionResult> UpdateTaskStatus(
            int taskId,
            [FromBody] UpdateTaskStatusDTO dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                await _studentService.UpdateTaskStatusAsync(taskId, GetCurrentUserId(), dto);
                return Ok(new { message = "Task status updated successfully" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        #endregion

        #region View Personal Statistics

        /// <summary>
        /// Get personal task and commit statistics
        /// Shows completed vs pending tasks, commit history, contribution metrics
        /// </summary>
        /// <param name="userId">Current user ID (will come from JWT claims in production)</param>
        /// <param name="projectId">Optional project ID to filter statistics</param>
        [HttpGet("statistics")]
        public async Task<IActionResult> GetPersonalStatistics(
            [FromQuery] int? projectId = null)
        {
            try
            {
                var statistics = await _studentService.GetPersonalStatisticsAsync(GetCurrentUserId(), projectId);
                return Ok(statistics);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Get task statistics grouped by status
        /// Shows count of tasks in each status: todo, in_progress, done
        /// </summary>
        /// <param name="userId">Current user ID (will come from JWT claims in production)</param>
        [HttpGet("statistics/tasks-by-status")]
        public async Task<IActionResult> GetTaskStatisticsByStatus()
        {
            try
            {
                var statistics = await _studentService.GetTaskStatisticsByStatusAsync(GetCurrentUserId());
                return Ok(statistics);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Get personal commit history
        /// Fetches commits from GitHub with timeline and contribution details
        /// </summary>
        /// <param name="userId">Current user ID (will come from JWT claims in production)</param>
        /// <param name="projectId">Optional project ID to filter commits</param>
        [HttpGet("commits")]
        public async Task<IActionResult> GetCommitHistory(
            [FromQuery] int? projectId = null)
        {
            try
            {
                var commits = await _studentService.GetCommitHistoryAsync(GetCurrentUserId(), projectId);
                return Ok(commits);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        #endregion

        #region Profile Management

        /// <summary>
        /// Get current student's profile information
        /// </summary>
        /// <param name="userId">Current user ID (will come from JWT claims in production)</param>
        [HttpGet("profile")]
        public async Task<IActionResult> GetMyProfile()
        {
            try
            {
                var profile = await _studentService.GetMyProfileAsync(GetCurrentUserId());
                return Ok(profile);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Update basic profile information (phone, GitHub username, Jira account)
        /// Students can only update specific fields, not role or status
        /// </summary>
        /// <param name="userId">Current user ID (will come from JWT claims in production)</param>
        /// <param name="dto">Update profile DTO</param>
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateMyProfile(
            [FromBody] UpdateProfileDTO dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var profile = await _studentService.UpdateMyProfileAsync(GetCurrentUserId(), dto);
                return Ok(profile);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        #endregion

        #region SRS Documents

        /// <summary>
        /// Get SRS documents for a specific project
        /// Students can only access documents for projects they are members of
        /// </summary>
        /// <param name="projectId">Project ID</param>
        /// <param name="userId">Current user ID (will come from JWT claims in production)</param>
        [HttpGet("projects/{projectId}/srs-documents")]
        public async Task<IActionResult> GetSrsDocumentsByProject(int projectId)
        {
            try
            {
                var documents = await _studentService.GetSrsDocumentsByProjectAsync(projectId, GetCurrentUserId());
                return Ok(documents);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Get a specific SRS document by ID
        /// Export/download functionality for SRS documents
        /// </summary>
        /// <param name="documentId">Document ID</param>
        /// <param name="userId">Current user ID (will come from JWT claims in production)</param>
        [HttpGet("srs-documents/{documentId}")]
        public async Task<IActionResult> GetSrsDocumentById(int documentId)
        {
            try
            {
                var document = await _studentService.GetSrsDocumentByIdAsync(documentId, GetCurrentUserId());

                if (document == null)
                {
                    return NotFound(new { message = "SRS document not found" });
                }

                return Ok(document);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Download SRS document file
        /// Returns the actual file if FilePath exists
        /// Security: Validates file path to prevent directory traversal attacks
        /// </summary>
        /// <param name="documentId">Document ID</param>
        /// <param name="userId">Current user ID (will come from JWT claims in production)</param>
        [HttpGet("srs-documents/{documentId}/download")]
        public async Task<IActionResult> DownloadSrsDocument(int documentId)
        {
            try
            {
                var document = await _studentService.GetSrsDocumentByIdAsync(documentId, GetCurrentUserId());

                if (document == null)
                {
                    return NotFound(new { message = "SRS document not found" });
                }

                if (string.IsNullOrEmpty(document.FilePath))
                {
                    return NotFound(new { message = "Document file path not configured" });
                }

                // Security: Validate file path to prevent directory traversal
                // Get the configured base directory for SRS documents from configuration
                // For now, using a safe default - should be moved to appsettings.json
                var baseDirectory = Path.Combine(Directory.GetCurrentDirectory(), "SrsDocuments");

                // Combine the base directory with the stored file path and normalize
                var fullPath = Path.GetFullPath(Path.Combine(baseDirectory, document.FilePath));

                // Ensure the resolved path is within the allowed base directory
                var normalizedBaseDirectory = Path.GetFullPath(baseDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!fullPath.StartsWith(normalizedBaseDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { message = "Invalid file path" });
                }

                if (!System.IO.File.Exists(fullPath))
                {
                    return NotFound(new { message = "Document file not found on server" });
                }

                var fileBytes = await System.IO.File.ReadAllBytesAsync(fullPath);
                var fileName = $"{document.DocumentTitle}_v{document.Version}.pdf";

                return File(fileBytes, "application/pdf", fileName);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        #endregion
    }
}
