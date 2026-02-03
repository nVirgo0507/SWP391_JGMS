using System;
using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs.Admin
{
    /// <summary>
    /// BR-056: Request DTO to update task status
    /// Used by team members to update their assigned task status
    /// </summary>
    public class UpdateTaskStatusDTO
    {
        public string? Title { get; set; }

        public string? Description { get; set; }

        public DateOnly? DueDate { get; set; }
    }

    /// <summary>
    /// BR-056: Response DTO for personal task statistics
    /// Shows task completion and progress metrics for a team member
    /// </summary>
    public class PersonalTaskStatisticResponseDTO
    {
        [Required]
        public int StatisticId { get; set; }

        [Required]
        public int UserId { get; set; }

        public string? UserName { get; set; }

        [Required]
        public int ProjectId { get; set; }

        public int TasksAssigned { get; set; }

        public int TasksCompleted { get; set; }

        public int TasksPending { get; set; }

        public double? CompletionPercentage { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// BR-056: Response DTO for personal commit statistics
    /// Shows commit activity metrics for a team member
    /// </summary>
    public class CommitStatisticResponseDTO
    {
        [Required]
        public int StatisticId { get; set; }

        [Required]
        public int UserId { get; set; }

        public string? UserName { get; set; }

        [Required]
        public int ProjectId { get; set; }

        public int TotalCommits { get; set; }

        public int CommitsThisWeek { get; set; }

        public int CommitsThisMonth { get; set; }

        public double? AverageCommitsPerDay { get; set; }

        public DateTime? LastCommitDate { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
