using System;
using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs.Admin
{
    /// <summary>
    /// Response DTO for SRS Document details
    /// </summary>
    public class SrsDocumentResponseDTO
    {
        public int DocumentId { get; set; }
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = null!;
        public string Version { get; set; } = null!;
        public string DocumentTitle { get; set; } = null!;
        public string? Introduction { get; set; }
        public string? Scope { get; set; }

        /// <summary>3.1 — Student-provided product perspective. Auto-generated if null.</summary>
        public string? ProductPerspective { get; set; }

        /// <summary>3.2 — Student-provided user classes description. Auto-generated if null.</summary>
        public string? UserClasses { get; set; }

        /// <summary>3.3 — Student-provided operating environment. Auto-generated if null.</summary>
        public string? OperatingEnvironment { get; set; }

        /// <summary>3.4 — Student-provided assumptions and dependencies. Auto-generated if null.</summary>
        public string? AssumptionsDependencies { get; set; }

        public string? FilePath { get; set; }
        public string Status { get; set; } = null!;
        public int GeneratedBy { get; set; }
        public string? GeneratedByName { get; set; }
        public DateTime? GeneratedAt { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<SrsIncludedRequirementDTO> Requirements { get; set; } = new();
    }

    /// <summary>
    /// DTO for an included requirement in the SRS document
    /// </summary>
    public class SrsIncludedRequirementDTO
    {
        public int RequirementId { get; set; }
        public string? SectionNumber { get; set; }
        public string? RequirementCode { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? RequirementType { get; set; }
        public string? Priority { get; set; }
    }

    /// <summary>
    /// Request DTO to generate an SRS document from existing requirements
    /// </summary>
    public class CreateSrsDocumentDTO
    {
        [Required]
        public string DocumentTitle { get; set; } = null!;

        /// <summary>Version string, e.g. "1.0", "2.0"</summary>
        public string Version { get; set; } = "1.0";

        /// <summary>Introduction section text (optional — auto-generated if empty)</summary>
        public string? Introduction { get; set; }

        /// <summary>Scope section text (optional — auto-generated if empty)</summary>
        public string? Scope { get; set; }

        /// <summary>
        /// 3.1 Product Perspective — describe what your system does and its context.
        /// Auto-generated from project/group name if not provided.
        /// </summary>
        public string? ProductPerspective { get; set; }

        /// <summary>
        /// 3.2 User Classes and Characteristics — describe the actual users of YOUR system
        /// (e.g. "Customer, Admin, Delivery Driver" for a food delivery app).
        /// Auto-generated with generic JGMS roles if not provided.
        /// </summary>
        public string? UserClasses { get; set; }

        /// <summary>
        /// 3.3 Operating Environment — describe your tech stack, deployment platform, browsers, etc.
        /// Auto-generated with a generic placeholder if not provided.
        /// </summary>
        public string? OperatingEnvironment { get; set; }

        /// <summary>
        /// 3.4 Assumptions and Dependencies — list what must be true for your system to work.
        /// Auto-generated with generic assumptions + Jira detection if not provided.
        /// </summary>
        public string? AssumptionsDependencies { get; set; }

        /// <summary>
        /// Optional list of requirement IDs to include.
        /// If empty/null, ALL requirements for the project are included.
        /// </summary>
        public List<int>? RequirementIds { get; set; }

        /// <summary>
        /// When true, automatically creates requirements from all synced Jira issues
        /// that don't already have a linked requirement. This saves you from having
        /// to manually create requirements one by one.
        /// Default: true
        /// </summary>
        public bool ImportFromJira { get; set; } = true;
    }

    /// <summary>
    /// Request DTO to update an SRS document
    /// </summary>
    public class UpdateSrsDocumentDTO
    {
        public string? DocumentTitle { get; set; }
        public string? Version { get; set; }
        public string? Introduction { get; set; }
        public string? Scope { get; set; }
        public string? ProductPerspective { get; set; }
        public string? UserClasses { get; set; }
        public string? OperatingEnvironment { get; set; }
        public string? AssumptionsDependencies { get; set; }

        /// <summary>Change status: "draft" or "published"</summary>
        public string? Status { get; set; }
    }

    /// <summary>
    /// Request DTO to regenerate the requirements snapshot of an existing SRS document.
    /// Re-runs requirement selection (and optionally Jira import) without creating a new version.
    /// </summary>
    public class RegenerateSrsDocumentDTO
    {
        /// <summary>
        /// Optional list of requirement IDs to include.
        /// If empty/null, ALL requirements for the project are included.
        /// </summary>
        public List<int>? RequirementIds { get; set; }

        /// <summary>
        /// When true, automatically creates requirements from all synced Jira issues
        /// that don't already have a linked requirement before regenerating.
        /// Default: true
        /// </summary>
        public bool ImportFromJira { get; set; } = true;
    }
}
