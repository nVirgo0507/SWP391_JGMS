using System;
using System.Collections.Generic;

namespace DAL.Models;

/// <summary>
/// Links specific requirements to an SRS version. Ensures traceability from Jira -&gt; Req -&gt; SRS.
/// </summary>
public partial class SrsIncludedRequirement
{
    public int Id { get; set; }

    public int DocumentId { get; set; }

    public int RequirementId { get; set; }

    /// <summary>
    /// e.g. 1.1, 2.0 - Order in document
    /// </summary>
    public string? SectionNumber { get; set; }

    /// <summary>
    /// Title at time of generation
    /// </summary>
    public string? SnapshotTitle { get; set; }

    /// <summary>
    /// Description at time of generation
    /// </summary>
    public string? SnapshotDescription { get; set; }

    public virtual SrsDocument Document { get; set; } = null!;

    public virtual Requirement Requirement { get; set; } = null!;
}
