﻿using System;
using System.Collections.Generic;

namespace DAL.Models;

/// <summary>
/// One project per group
/// </summary>
public partial class Project
{
    public int ProjectId { get; set; }

    public int GroupId { get; set; }

    public string ProjectName { get; set; } = null!;

    public string? Description { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public ProjectStatus? Status { get; set; } = ProjectStatus.active;

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<CommitStatistic> CommitStatistics { get; set; } = new List<CommitStatistic>();

    public virtual ICollection<Commit> Commits { get; set; } = new List<Commit>();

    public virtual ICollection<GithubCommit> GithubCommits { get; set; } = new List<GithubCommit>();

    public virtual GithubIntegration? GithubIntegration { get; set; }

    public virtual StudentGroup Group { get; set; } = null!;

    public virtual JiraIntegration? JiraIntegration { get; set; }

    public virtual ICollection<JiraIssue> JiraIssues { get; set; } = new List<JiraIssue>();

    public virtual ICollection<PersonalTaskStatistic> PersonalTaskStatistics { get; set; } = new List<PersonalTaskStatistic>();

    public virtual ICollection<ProgressReport> ProgressReports { get; set; } = new List<ProgressReport>();

    public virtual ICollection<Requirement> Requirements { get; set; } = new List<Requirement>();

    public virtual ICollection<SrsDocument> SrsDocuments { get; set; } = new List<SrsDocument>();

    public virtual ICollection<TeamCommitSummary> TeamCommitSummaries { get; set; } = new List<TeamCommitSummary>();
}
