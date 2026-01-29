using System;
using System.Collections.Generic;

namespace DAL.Models;

/// <summary>
/// Problem 2: Tổng hợp báo cáo phân công và thực hiện công việc | Lecturer: view project progress reports
/// </summary>
public partial class ProgressReport
{
    public int ReportId { get; set; }

    public int ProjectId { get; set; }

    public DateOnly? ReportPeriodStart { get; set; }

    public DateOnly? ReportPeriodEnd { get; set; }

    public string ReportData { get; set; } = null!;

    public string? Summary { get; set; }

    public string? FilePath { get; set; }

    public int GeneratedBy { get; set; }

    public DateTime GeneratedAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User GeneratedByNavigation { get; set; } = null!;

    public virtual Project Project { get; set; } = null!;
}
