using System;
using System.Collections.Generic;

namespace DAL.Models;

/// <summary>
/// Raw commits synced from GitHub API
/// </summary>
public partial class GithubCommit
{
    public int GithubCommitId { get; set; }

    public int ProjectId { get; set; }

    public string CommitSha { get; set; } = null!;

    public string AuthorUsername { get; set; } = null!;

    public string? AuthorEmail { get; set; }

    public string? CommitMessage { get; set; }

    public int? Additions { get; set; }

    public int? Deletions { get; set; }

    public int? ChangedFiles { get; set; }

    public DateTime CommitDate { get; set; }

    public string? BranchName { get; set; }

    public DateTime? LastSynced { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Commit? Commit { get; set; }

    public virtual Project Project { get; set; } = null!;
}
