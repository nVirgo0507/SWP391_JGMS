using System;
using System.Collections.Generic;

namespace DAL.Models;

/// <summary>
/// Header record for the SRS. Content is built by joining with SRS_INCLUDED_REQUIREMENT
/// </summary>
public partial class SrsDocument
{
    public int DocumentId { get; set; }

    public int ProjectId { get; set; }

    public string Version { get; set; } = null!;

    public string DocumentTitle { get; set; } = null!;

    public string? Introduction { get; set; }

    public string? Scope { get; set; }

    public string? FilePath { get; set; }

    public int GeneratedBy { get; set; }

    public DateTime? GeneratedAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual User GeneratedByNavigation { get; set; } = null!;

    public virtual Project Project { get; set; } = null!;

    public virtual ICollection<SrsIncludedRequirement> SrsIncludedRequirements { get; set; } = new List<SrsIncludedRequirement>();
}
