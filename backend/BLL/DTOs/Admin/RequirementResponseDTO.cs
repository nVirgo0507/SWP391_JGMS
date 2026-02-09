using System;
using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs.Admin
{
    /// <summary>
    /// BR-055: Response DTO for Requirement details
    /// Used when team leader manages requirements for their group's project
    /// </summary>
    public class RequirementResponseDTO
    {
        [Required]
        public int RequirementId { get; set; }

        [Required]
        public int ProjectId { get; set; }

        public int? JiraIssueId { get; set; }

        [Required]
        public string RequirementCode { get; set; } = null!;

        [Required]
        public string Title { get; set; } = null!;

        public string? Description { get; set; }

        public int CreatedBy { get; set; }

        public string? CreatedByName { get; set; }

        public DateTime? CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// BR-055: Request DTO to create a requirement
    /// </summary>
    public class CreateRequirementDTO
    {
        [Required]
        public string RequirementCode { get; set; } = null!;

        [Required]
        public string Title { get; set; } = null!;

        public string? Description { get; set; }

        public int? JiraIssueId { get; set; }
    }

    /// <summary>
    /// BR-055: Request DTO to update a requirement
    /// </summary>
    public class UpdateRequirementDTO
    {
        public string? Title { get; set; }

        public string? Description { get; set; }

        public int? JiraIssueId { get; set; }
    }
}
