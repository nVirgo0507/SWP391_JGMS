using System;
using System.Collections.Generic;

namespace DAL.Models;

/// <summary>
/// Commits linked to students (matched by github_username) - base data for all commit reports
/// </summary>
public partial class Commit
{
    public int CommitId { get; set; }

    public int UserId { get; set; }

    public int GithubCommitId { get; set; }

    public int ProjectId { get; set; }

    public string? CommitMessage { get; set; }

    public int? Additions { get; set; }

    public int? Deletions { get; set; }

    public int? ChangedFiles { get; set; }

    public DateTime CommitDate { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual GithubCommit GithubCommit { get; set; } = null!;

    public virtual Project Project { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
