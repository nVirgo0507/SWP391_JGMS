using System;
using System.Collections.Generic;

namespace DAL.Models;

/// <summary>
/// Team Member: view personal task statistics
/// </summary>
public partial class PersonalTaskStatistic
{
    public int StatId { get; set; }

    public int UserId { get; set; }

    public int ProjectId { get; set; }

    public int? TotalTasks { get; set; }

    public int? CompletedTasks { get; set; }

    public int? InProgressTasks { get; set; }

    public int? OverdueTasks { get; set; }

    public decimal? CompletionRate { get; set; }

    public DateTime? LastCalculated { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Project Project { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
