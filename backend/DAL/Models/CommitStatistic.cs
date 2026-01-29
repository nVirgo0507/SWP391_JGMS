using System;
using System.Collections.Generic;

namespace DAL.Models;

/// <summary>
/// Problem 3: Báo cáo đánh giá tần suất và chất lượng các lần commit | Lecturer: view GitHub commit statistics | Team Member: view personal commit statistics
/// </summary>
public partial class CommitStatistic
{
    public int StatId { get; set; }

    public int ProjectId { get; set; }

    public int UserId { get; set; }

    public DateOnly PeriodStart { get; set; }

    public DateOnly PeriodEnd { get; set; }

    public int? TotalCommits { get; set; }

    public int? TotalAdditions { get; set; }

    public int? TotalDeletions { get; set; }

    public int? TotalChangedFiles { get; set; }

    public decimal? CommitFrequency { get; set; }

    public int? AvgCommitSize { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Project Project { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
