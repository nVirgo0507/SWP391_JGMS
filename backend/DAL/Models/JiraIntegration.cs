﻿using System;
using System.Collections.Generic;

namespace DAL.Models;

/// <summary>
/// Admin: configure Jira integration
/// </summary>
public partial class JiraIntegration
{
    public int IntegrationId { get; set; }

    public int ProjectId { get; set; }

    public string JiraUrl { get; set; } = null!;

    /// <summary>
    /// Encrypted token
    /// </summary>
    public string ApiToken { get; set; } = null!;

    public string JiraEmail { get; set; } = null!;

    public string ProjectKey { get; set; } = null!;

    public DateTime? LastSync { get; set; }

    public SyncStatus SyncStatus { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Project Project { get; set; } = null!;
}
