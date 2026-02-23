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
