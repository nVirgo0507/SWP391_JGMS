﻿using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs.Student
{
    /// <summary>
    /// DTO for updating task status
    /// </summary>
    public class UpdateTaskStatusDTO
    {
        /// <summary>
        /// Task status. Accepts multiple formats (case-insensitive):
        /// - "todo", "To Do", "to do", "to_do"
        /// - "in_progress", "In Progress", "in progress", "in-progress"
        /// - "done", "Done"
        /// Spaces and hyphens are automatically converted to underscores.
        /// </summary>
        /// <example>in_progress</example>
        [Required]
        public string Status { get; set; } = null!;

        /// <summary>
        /// Optional comment about the status update.
        /// TODO: This will be synced to Jira as a comment when Jira integration is implemented.
        /// Currently not stored in database - reserved for future use.
        /// </summary>
        public string? Comment { get; set; }

        /// <summary>
        /// Optional work hours spent on the task.
        /// TODO: This will be logged in Jira when integration is implemented.
        /// Currently not stored in database - reserved for future use.
        /// </summary>
        public int? WorkHours { get; set; }
    }
}
