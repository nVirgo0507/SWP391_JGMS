using System;
using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs.Admin
{
    /// <summary>
    /// BR-055: Response DTO for Project details
    /// Used when team leader views/manages their group's project
    /// </summary>
    public class ProjectResponseDTO
    {
        [Required]
        public int ProjectId { get; set; }

        [Required]
        public int GroupId { get; set; }

        public string? GroupCode { get; set; }

        public string? GroupName { get; set; }

        [Required]
        public string ProjectName { get; set; } = null!;

        public string? Description { get; set; }

        public DateOnly? StartDate { get; set; }

        public DateOnly? EndDate { get; set; }

        public string? Status { get; set; }

        public DateTime? CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
