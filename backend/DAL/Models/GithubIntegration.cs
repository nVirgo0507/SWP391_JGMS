using System;
using System.Collections.Generic;

namespace DAL.Models;

/// <summary>
/// Admin: configure GitHub integration
/// </summary>
public partial class GithubIntegration
{
    public int IntegrationId { get; set; }

    public int ProjectId { get; set; }

    public string RepoUrl { get; set; } = null!;

    /// <summary>
    /// Encrypted token
    /// </summary>
    public string ApiToken { get; set; } = null!;

    public string RepoOwner { get; set; } = null!;

    public string RepoName { get; set; } = null!;

    public DateTime? LastSync { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Project Project { get; set; } = null!;
}
