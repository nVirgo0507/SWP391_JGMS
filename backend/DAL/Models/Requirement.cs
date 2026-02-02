using System;
using System.Collections.Generic;

namespace DAL.Models;

/// <summary>
/// Team Leader: manage group requirements (synced from Jira) | Lecturer: view requirements
/// </summary>
public partial class Requirement
{
    public int RequirementId { get; set; }

    public int ProjectId { get; set; }

    public int? JiraIssueId { get; set; }

    public string RequirementCode { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public int CreatedBy { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual JiraIssue? JiraIssue { get; set; }

    public virtual Project Project { get; set; } = null!;

    public virtual ICollection<SrsIncludedRequirement> SrsIncludedRequirements { get; set; } = new List<SrsIncludedRequirement>();

    public virtual ICollection<Task> Tasks { get; set; } = new List<Task>();
}
