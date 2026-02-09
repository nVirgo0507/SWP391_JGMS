using System;
using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs.Admin
{
    /// <summary>
    /// BR-055: Response DTO for SRS Document details
    /// Used when team leader manages SRS for their group's project
    /// </summary>
    public class SrsDocumentResponseDTO
    {
        [Required]
        public int SrsId { get; set; }

        [Required]
        public int ProjectId { get; set; }

        [Required]
        public string DocumentName { get; set; } = null!;

        public string? Content { get; set; }

        public DateTime? CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// BR-055: Request DTO to create an SRS document
    /// </summary>
    public class CreateSrsDocumentDTO
    {
        [Required]
        public string DocumentName { get; set; } = null!;

        public string? Content { get; set; }
    }

    /// <summary>
    /// BR-055: Request DTO to update an SRS document
    /// </summary>
    public class UpdateSrsDocumentDTO
    {
        public string? DocumentName { get; set; }

        public string? Content { get; set; }
    }
}
