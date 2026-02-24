using System;
using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs.Admin
{
    /// <summary>
    /// BR-055: Response DTO for Task details
    /// Used when team leader manages tasks for their group
    /// </summary>
    public class TaskResponseDTO
    {
        [Required]
        public int TaskId { get; set; }

        public int? RequirementId { get; set; }

        public int? JiraIssueId { get; set; }

        public int? AssignedTo { get; set; }

        public string? AssignedToName { get; set; }

        [Required]
        public string Title { get; set; } = null!;

        public string? Description { get; set; }

        public DateOnly? DueDate { get; set; }

        public DateTime? CompletedAt { get; set; }

        public DateTime? CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// BR-055: Request DTO to create a task
    /// </summary>
    public class CreateTaskDTO
    {
        [Required]
        public string Title { get; set; } = null!;

        public string? Description { get; set; }

        public int? RequirementId { get; set; }

        /// <summary>
        /// Optional: link this task to a synced Jira issue (JGMS internal JiraIssueId, not the Jira issue key).
        /// When provided, the task title, description, and priority are pre-filled from the Jira issue
        /// unless explicitly overridden in the request.
        /// </summary>
        public int? JiraIssueId { get; set; }

        public DateOnly? DueDate { get; set; }
    }

    /// <summary>
    /// Request DTO to create a task directly from a synced Jira issue key (e.g. "SWP391-5").
    /// The task title, description, and priority are populated automatically from the issue.
    /// </summary>
    public class CreateTaskFromJiraIssueDTO
    {
        /// <summary>Jira issue key as it appears in Jira, e.g. "SWP391-5"</summary>
        [Required]
        public string IssueKey { get; set; } = null!;

        /// <summary>Optional override for the task title (defaults to the Jira issue summary)</summary>
        public string? TitleOverride { get; set; }

        /// <summary>Optional: assign immediately to a group member's userId</summary>
        public int? AssignedTo { get; set; }

        public DateOnly? DueDate { get; set; }
    }

    /// <summary>
    /// BR-055: Request DTO to update a task
    /// </summary>
    public class UpdateTaskDTO
    {
        public string? Title { get; set; }

        public string? Description { get; set; }

        public DateOnly? DueDate { get; set; }

        public DateTime? CompletedAt { get; set; }
    }
}
