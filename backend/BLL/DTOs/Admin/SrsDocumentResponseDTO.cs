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

        /// <summary>Change status: "draft" or "published"</summary>
        public string? Status { get; set; }
    }
}
