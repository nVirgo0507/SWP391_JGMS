using System;
using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs.Admin
{
    /// <summary>
    /// BR-055: Response DTO for Task details
    /// </summary>
    public class TaskResponseDTO
    {
        [Required]
        public int TaskId { get; set; }

        public int? RequirementId { get; set; }

        public string? RequirementCode { get; set; }

        public int? JiraIssueId { get; set; }

        /// <summary>Jira issue key e.g. "SWP391-5"</summary>
        public string? JiraIssueKey { get; set; }

        public int? AssignedTo { get; set; }

        public string? AssignedToName { get; set; }

        [Required]
        public string Title { get; set; } = null!;

        public string? Description { get; set; }

        /// <summary>todo | in_progress | done</summary>
        public string Status { get; set; } = "todo";

        /// <summary>high | medium | low</summary>
        public string Priority { get; set; } = "medium";

        public DateOnly? DueDate { get; set; }

        public DateTime? CompletedAt { get; set; }

        /// <summary>Jira sprint ID if the linked issue belongs to a sprint</summary>
        public int? SprintId { get; set; }

        public string? SprintName { get; set; }

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

        /// <summary>Link to an existing synced Jira issue (internal JiraIssueId)</summary>
        public int? JiraIssueId { get; set; }

        /// <summary>Assign immediately to a group member userId</summary>
        public int? AssignedTo { get; set; }

        /// <summary>todo | in_progress | done — defaults to todo</summary>
        public string Status { get; set; } = "todo";

        /// <summary>high | medium | low — defaults to medium</summary>
        public string Priority { get; set; } = "medium";

        public DateOnly? DueDate { get; set; }

        /// <summary>
        /// Optional Jira sprint ID to add the linked issue to a sprint when creating via Jira.
        /// Requires Jira integration to be configured.
        /// </summary>
        public int? SprintId { get; set; }
    }

    /// <summary>
    /// Request DTO to create a task directly from a synced Jira issue key (e.g. "SWP391-5").
    /// Title, description, and priority are populated automatically from the issue.
    /// </summary>
    public class CreateTaskFromJiraIssueDTO
    {
        /// <summary>
        /// Jira issue identifier — accepts either:
        /// - The Jira issue key (e.g. "SWP391-5")
        /// - The internal numeric JiraIssueId from the local database (e.g. "10")
        /// </summary>
        [Required]
        public string IssueKey { get; set; } = null!;

        /// <summary>Optional override for the task title (defaults to the Jira issue summary)</summary>
        public string? TitleOverride { get; set; }

        /// <summary>Optional: assign immediately to a group member userId</summary>
        public int? AssignedTo { get; set; }

        public DateOnly? DueDate { get; set; }

        /// <summary>Optional Jira sprint ID to move the issue into a sprint</summary>
        public int? SprintId { get; set; }
    }

    /// <summary>
    /// BR-055: Request DTO to update a task — all fields optional.
    /// Changes are synced back to Jira if the task is linked to a Jira issue.
    /// </summary>
    public class UpdateTaskDTO
    {
        public string? Title { get; set; }

        public string? Description { get; set; }

        /// <summary>Change assignee to another group member userId</summary>
        public int? AssignedTo { get; set; }

        /// <summary>todo | in_progress | done</summary>
        public string? Status { get; set; }

        /// <summary>high | medium | low</summary>
        public string? Priority { get; set; }

        public DateOnly? DueDate { get; set; }

        public DateTime? CompletedAt { get; set; }

        /// <summary>Link or re-link to a requirement</summary>
        public int? RequirementId { get; set; }
    }

    /// <summary>
    /// Move a task's linked Jira issue to a different sprint
    /// </summary>
    public class MoveTaskToSprintDTO
    {
        /// <summary>Target Jira sprint ID (integer). Use 0 to remove from all sprints (backlog).</summary>
        [Required]
        public int SprintId { get; set; }
    }

    /// <summary>
    /// Link a task to a requirement (creates a relationship locally and in Jira via issue link)
    /// </summary>
    public class LinkTaskToRequirementDTO
    {
        /// <summary>The local RequirementId to link this task to</summary>
        [Required]
        public int RequirementId { get; set; }
    }
}
