using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs.Admin
{
    /// <summary>
    /// Read-only progress report payload for lecturer dashboards.
    /// </summary>
    public class ProgressReportResponseDTO
    {
        [Required]
        public int ReportId { get; set; }

        [Required]
        public int ProjectId { get; set; }

        public DateOnly? ReportPeriodStart { get; set; }

        public DateOnly? ReportPeriodEnd { get; set; }

        public string ReportData { get; set; } = string.Empty;

        public string? Summary { get; set; }

        public string? FilePath { get; set; }

        [Required]
        public int GeneratedBy { get; set; }

        public string? GeneratedByName { get; set; }

        public DateTime GeneratedAt { get; set; }

        public DateTime? CreatedAt { get; set; }
    }

    /// <summary>
    /// Aggregated GitHub commit statistics for one group/project.
    /// </summary>
    public class GroupCommitStatisticsResponseDTO
    {
        public int GroupId { get; set; }

        public string? GroupCode { get; set; }

        public int? ProjectId { get; set; }

        public DateOnly? PeriodStart { get; set; }

        public DateOnly? PeriodEnd { get; set; }

        public int TotalCommits { get; set; }

        public int TotalAdditions { get; set; }

        public int TotalDeletions { get; set; }

        public int TotalChangedFiles { get; set; }

        public List<MemberCommitStatisticsDTO> Members { get; set; } = new();
    }

    public class MemberCommitStatisticsDTO
    {
        public int UserId { get; set; }

        public string UserName { get; set; } = string.Empty;

        public int TotalCommits { get; set; }

        public int TotalAdditions { get; set; }

        public int TotalDeletions { get; set; }

        public int TotalChangedFiles { get; set; }

        public decimal? CommitFrequency { get; set; }

        public int? AvgCommitSize { get; set; }

        public DateTime? LastCommitDate { get; set; }
    }
}

