using System;
using System.Collections.Generic;

namespace DAL.Models;

/// <summary>
/// Team Leader: view team commit summaries (aggregated from COMMIT_STATISTICS)
/// </summary>
public partial class TeamCommitSummary
{
    public int SummaryId { get; set; }

    public int ProjectId { get; set; }

    public DateOnly SummaryDate { get; set; }

    public int? TotalCommits { get; set; }

    public int? TotalAdditions { get; set; }

    public int? TotalDeletions { get; set; }

    public int? ActiveContributors { get; set; }

    public string? SummaryData { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Project Project { get; set; } = null!;
}
