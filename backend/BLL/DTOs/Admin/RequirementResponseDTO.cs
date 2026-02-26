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

        /// <summary>The Jira issue key (e.g. "SWP391-5"), populated if linked to Jira</summary>
        public string? JiraIssueKey { get; set; }

        [Required]
        public string RequirementCode { get; set; } = null!;

        [Required]
        public string Title { get; set; } = null!;

        public string? Description { get; set; }

        /// <summary>Requirement type: functional | non_functional</summary>
        public string? RequirementType { get; set; }

        /// <summary>Jira issue type representing hierarchy level: Epic, Story, Task, Sub-task, Bug</summary>
        public string? IssueType { get; set; }

        /// <summary>Priority: Highest, High, Medium, Low, Lowest</summary>
        public string? Priority { get; set; }

        /// <summary>Current status synced from Jira (e.g. To Do, In Progress, Done)</summary>
        public string? JiraStatus { get; set; }

        public int CreatedBy { get; set; }

        public string? CreatedByName { get; set; }

        public DateTime? CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// BR-055: Request DTO to create a requirement in Jira and locally
    /// </summary>
    public class CreateRequirementDTO
    {
        [Required]
        public string RequirementCode { get; set; } = null!;

        [Required]
        public string Title { get; set; } = null!;

        public string? Description { get; set; }

        /// <summary>
        /// Requirement type: functional | non_functional
        /// Defaults to "functional"
        /// </summary>
        public string RequirementType { get; set; } = "functional";

        /// <summary>
        /// Jira issue type for hierarchy: Epic | Story | Task | Sub-task | Bug
        /// Defaults to "Story" if not specified
        /// </summary>
        public string IssueType { get; set; } = "Story";

        /// <summary>
        /// Priority: Highest | High | Medium | Low | Lowest
        /// Defaults to "Medium"
        /// </summary>
        public string Priority { get; set; } = "Medium";

        /// <summary>Link to an existing synced Jira issue instead of creating a new one</summary>
        public int? JiraIssueId { get; set; }
    }

    /// <summary>
    /// BR-055: Request DTO to update a requirement and sync changes back to Jira
    /// </summary>
    public class UpdateRequirementDTO
    {
        public string? Title { get; set; }

        public string? Description { get; set; }

        /// <summary>Requirement type: functional | non_functional</summary>
        public string? RequirementType { get; set; }

        /// <summary>Priority: Highest | High | Medium | Low | Lowest</summary>
        public string? Priority { get; set; }

        public int? JiraIssueId { get; set; }
    }

    /// <summary>
    /// BR-055: Request DTO to reorder/organize requirements hierarchy
    /// Allows setting parent-child relationships (Epic → Story → Task)
    /// </summary>
    public class ReorderRequirementsDTO
    {
        [Required]
        public List<RequirementHierarchyItemDTO> Items { get; set; } = new();
    }

    public class RequirementHierarchyItemDTO
    {
        [Required]
        public int RequirementId { get; set; }

        /// <summary>Display order index (0-based)</summary>
        [Required]
        public int Order { get; set; }
    }
}
