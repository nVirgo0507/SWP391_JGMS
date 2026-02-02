﻿using DAL.Models;

namespace BLL.DTOs.Student
{
    /// <summary>
    /// Response DTO for task information
    /// </summary>
    public class TaskResponseDTO
    {
        public int TaskId { get; set; }
        public int? RequirementId { get; set; }
        public int? JiraIssueId { get; set; }
        public int? AssignedTo { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public string Status { get; set; } = null!;
        public string Priority { get; set; } = null!;
        public DateOnly? DueDate { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        
        // Additional info from navigation properties
        public string? AssignedToName { get; set; }
        public string? JiraIssueKey { get; set; }
        public string? JiraStatus { get; set; }
    }
}
