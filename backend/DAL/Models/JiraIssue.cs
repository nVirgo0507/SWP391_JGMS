﻿using System;
using System.Collections.Generic;

namespace DAL.Models;

/// <summary>
/// Raw issues synced from Jira - source for requirements management
/// </summary>
public partial class JiraIssue
{
    public int JiraIssueId { get; set; }

    public int ProjectId { get; set; }

    public string IssueKey { get; set; } = null!;

    public string JiraId { get; set; } = null!;

    public string IssueType { get; set; } = null!;

    public string Summary { get; set; } = null!;

    public string? Description { get; set; }

    public JiraPriority? Priority { get; set; }

    public string Status { get; set; } = null!;

    public string? AssigneeJiraId { get; set; }

    public DateTime? CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public DateTime? LastSynced { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Project Project { get; set; } = null!;

    public virtual Requirement? Requirement { get; set; }

    public virtual Task? Task { get; set; }
}
