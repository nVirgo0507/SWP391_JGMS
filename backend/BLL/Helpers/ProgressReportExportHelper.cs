using DAL.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text;
using System.Text.Json;

namespace BLL.Helpers
{
    public static class ProgressReportExportHelper
    {
        public static byte[] GenerateProgressReportPdf(StudentGroup group, Project project, ProgressReport report)
        {
            var parsed = ParseReportData(report.ReportData);
            var snapshot = BuildWeeklySnapshot(parsed.AutoTaskProgress);

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);

                    page.Header().Column(column =>
                    {
                        column.Item().Text("Project Progress Report").Bold().FontSize(18);
                        if (!string.IsNullOrWhiteSpace(parsed.Title))
                        {
                            column.Item().Text(parsed.Title).FontSize(13).SemiBold();
                        }
                        column.Item().Text($"Group: {group.GroupCode} - {group.GroupName}").FontSize(11);
                        column.Item().Text($"Project: {project.ProjectName}").FontSize(11);
                        column.Item().Text($"Report ID: {report.ReportId} | Type: {report.ReportType}").FontSize(10);
                    });

                    page.Content().PaddingVertical(10).Column(column =>
                    {
                        column.Spacing(8);

                        column.Item().Text($"Generated At: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC").FontSize(10);

                        if (snapshot.Any())
                        {
                            column.Item().Text("Weekly Snapshot").Bold().FontSize(12);
                            column.Item().Row(row =>
                            {
                                foreach (var metric in snapshot)
                                {
                                    row.RelativeItem().Border(1).Padding(6).Column(card =>
                                    {
                                        card.Item().Text(metric.Key).FontSize(9).SemiBold();
                                        card.Item().Text(metric.Value).FontSize(12).Bold();
                                    });
                                }
                            });
                        }

                        if (report.ReportPeriodStart.HasValue || report.ReportPeriodEnd.HasValue)
                        {
                            var start = report.ReportPeriodStart?.ToString("yyyy-MM-dd") ?? "N/A";
                            var end = report.ReportPeriodEnd?.ToString("yyyy-MM-dd") ?? "N/A";
                            column.Item().Text($"Period: {start} to {end}").FontSize(10);
                        }

                        if (!string.IsNullOrWhiteSpace(report.Summary))
                        {
                            column.Item().Text("Summary").Bold().FontSize(12);
                            column.Item().Text(report.Summary).FontSize(10);
                        }

                        if (!string.IsNullOrWhiteSpace(parsed.Notes))
                        {
                            column.Item().Text("Notes").Bold().FontSize(12);
                            column.Item().Text(parsed.Notes).FontSize(10);
                        }

                        if (parsed.AutoTaskProgress.Any())
                        {
                            column.Item().Text("Task Progress").Bold().FontSize(12);
                            foreach (var metric in parsed.AutoTaskProgress)
                            {
                                column.Item().Text($"- {metric.Key}: {metric.Value}").FontSize(10);
                            }
                        }

                        if (parsed.BulletHighlights.Any())
                        {
                            column.Item().Text("Key Highlights").Bold().FontSize(12);
                            foreach (var item in parsed.BulletHighlights)
                            {
                                column.Item().Text($"- {item}").FontSize(10);
                            }
                        }

                        if (parsed.Highlights.Any())
                        {
                            column.Item().Text("Highlights").Bold().FontSize(12);
                            foreach (var metric in parsed.Highlights)
                            {
                                column.Item().Text($"- {metric.Key}: {metric.Value}").FontSize(10);
                            }
                        }

                        foreach (var section in parsed.Sections)
                        {
                            column.Item().Text(section.Title).Bold().FontSize(12);

                            foreach (var row in section.Rows)
                            {
                                column.Item().Text($"- {row.Key}: {row.Value}").FontSize(10);
                            }

                            foreach (var item in section.Items)
                            {
                                column.Item().Text($"- {item}").FontSize(10);
                            }
                        }

                    });

                    page.Footer().AlignRight().Text(x =>
                    {
                        x.Span("Page ");
                        x.CurrentPageNumber();
                        x.Span(" of ");
                        x.TotalPages();
                    });
                });
            }).GeneratePdf();
        }

        public static string GenerateProgressReportWordHtml(StudentGroup group, Project project, ProgressReport report)
        {
            var parsed = ParseReportData(report.ReportData);
            var snapshot = BuildWeeklySnapshot(parsed.AutoTaskProgress);
            var start = report.ReportPeriodStart?.ToString("yyyy-MM-dd") ?? "N/A";
            var end = report.ReportPeriodEnd?.ToString("yyyy-MM-dd") ?? "N/A";

            var sb = new StringBuilder();
            sb.AppendLine("<html xmlns:o=\"urn:schemas-microsoft-com:office:office\" xmlns:w=\"urn:schemas-microsoft-com:office:word\" xmlns=\"http://www.w3.org/TR/REC-html40\">");
            sb.AppendLine("<head><meta charset=\"UTF-8\"><title>Progress Report</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("body { font-family: Calibri, Arial, sans-serif; font-size: 11pt; }");
            sb.AppendLine("h1 { font-size: 18pt; margin-bottom: 0; }");
            sb.AppendLine("h2 { font-size: 13pt; margin-top: 20px; }");
            sb.AppendLine("h3 { font-size: 11.5pt; margin-top: 16px; } ");
            sb.AppendLine(".meta { margin-top: 8px; color: #333; }");
            sb.AppendLine("table { border-collapse: collapse; width: 100%; margin-top: 8px; }");
            sb.AppendLine("th, td { border: 1px solid #d0d7de; padding: 6px; text-align: left; vertical-align: top; }");
            sb.AppendLine("th { background: #f3f4f6; width: 30%; }");
            sb.AppendLine("ul { margin: 6px 0 0 20px; }");
            sb.AppendLine(".cards { width: 100%; border-collapse: separate; border-spacing: 8px; margin-top: 8px; }");
            sb.AppendLine(".card { border: 1px solid #d0d7de; padding: 8px; border-radius: 6px; }");
            sb.AppendLine(".card-label { color: #4b5563; font-size: 10pt; }");
            sb.AppendLine(".card-value { font-size: 14pt; font-weight: 700; }");
            sb.AppendLine("</style></head><body>");
            sb.AppendLine("<h1>Project Progress Report</h1>");
            if (!string.IsNullOrWhiteSpace(parsed.Title))
                sb.AppendLine($"<h2>{Escape(parsed.Title)}</h2>");
            sb.AppendLine($"<div class='meta'><strong>Group:</strong> {Escape(group.GroupCode)} - {Escape(group.GroupName)}</div>");
            sb.AppendLine($"<div class='meta'><strong>Project:</strong> {Escape(project.ProjectName)}</div>");
            sb.AppendLine($"<div class='meta'><strong>Report ID:</strong> {report.ReportId}</div>");
            sb.AppendLine($"<div class='meta'><strong>Type:</strong> {report.ReportType}</div>");
            sb.AppendLine($"<div class='meta'><strong>Period:</strong> {start} to {end}</div>");
            sb.AppendLine($"<div class='meta'><strong>Generated At:</strong> {report.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC</div>");

            if (snapshot.Any())
            {
                sb.AppendLine("<h2>Weekly Snapshot</h2>");
                sb.AppendLine("<table class='cards'><tr>");
                foreach (var metric in snapshot)
                {
                    sb.AppendLine("<td class='card'>");
                    sb.AppendLine($"<div class='card-label'>{Escape(metric.Key)}</div>");
                    sb.AppendLine($"<div class='card-value'>{Escape(metric.Value)}</div>");
                    sb.AppendLine("</td>");
                }
                sb.AppendLine("</tr></table>");
            }

            if (!string.IsNullOrWhiteSpace(report.Summary))
            {
                sb.AppendLine("<h2>Summary</h2>");
                sb.AppendLine($"<p>{Escape(report.Summary)}</p>");
            }

            if (!string.IsNullOrWhiteSpace(parsed.Notes))
            {
                sb.AppendLine("<h2>Notes</h2>");
                sb.AppendLine($"<p>{Escape(parsed.Notes)}</p>");
            }

            if (parsed.AutoTaskProgress.Any())
            {
                sb.AppendLine("<h2>Task Progress</h2>");
                sb.AppendLine("<table><tbody>");
                foreach (var metric in parsed.AutoTaskProgress)
                {
                    sb.AppendLine($"<tr><th>{Escape(metric.Key)}</th><td>{Escape(metric.Value)}</td></tr>");
                }
                sb.AppendLine("</tbody></table>");
            }

            if (parsed.BulletHighlights.Any())
            {
                sb.AppendLine("<h2>Key Highlights</h2>");
                sb.AppendLine("<ul>");
                foreach (var item in parsed.BulletHighlights)
                {
                    sb.AppendLine($"<li>{Escape(item)}</li>");
                }
                sb.AppendLine("</ul>");
            }

            if (parsed.Highlights.Any())
            {
                sb.AppendLine("<h2>Highlights</h2>");
                sb.AppendLine("<table><tbody>");
                foreach (var metric in parsed.Highlights)
                {
                    sb.AppendLine($"<tr><th>{Escape(metric.Key)}</th><td>{Escape(metric.Value)}</td></tr>");
                }
                sb.AppendLine("</tbody></table>");
            }

            if (parsed.Sections.Any())
            {
                sb.AppendLine("<h2>Detailed Data</h2>");

                foreach (var section in parsed.Sections)
                {
                    sb.AppendLine($"<h3>{Escape(section.Title)}</h3>");

                    if (section.Rows.Any())
                    {
                        sb.AppendLine("<table><tbody>");
                        foreach (var row in section.Rows)
                        {
                            sb.AppendLine($"<tr><th>{Escape(row.Key)}</th><td>{Escape(row.Value)}</td></tr>");
                        }
                        sb.AppendLine("</tbody></table>");
                    }

                    if (section.Items.Any())
                    {
                        sb.AppendLine("<ul>");
                        foreach (var item in section.Items)
                        {
                            sb.AppendLine($"<li>{Escape(item)}</li>");
                        }
                        sb.AppendLine("</ul>");
                    }
                }
            }

            sb.AppendLine("</body></html>");

            return sb.ToString();
        }

        private static List<ReportRow> BuildWeeklySnapshot(List<ReportRow> autoTaskProgress)
        {
            if (!autoTaskProgress.Any())
                return new List<ReportRow>();

            return new List<ReportRow>
            {
                new("Done", FindMetric(autoTaskProgress, "done")),
                new("In Progress", FindMetric(autoTaskProgress, "inprogress")),
                new("Todo", FindMetric(autoTaskProgress, "todo")),
                new("Completion", FindMetric(autoTaskProgress, "completionrate"))
            };
        }

        private static string FindMetric(List<ReportRow> metrics, string normalizedName)
        {
            static string Normalize(string value) => new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

            var match = metrics.FirstOrDefault(m => Normalize(m.Key) == normalizedName);
            return string.IsNullOrWhiteSpace(match?.Value) ? "N/A" : match.Value;
        }

        private static ParsedReportData ParseReportData(string reportData)
        {
            var result = new ParsedReportData();

            if (string.IsNullOrWhiteSpace(reportData))
                return result;

            try
            {
                using var doc = JsonDocument.Parse(reportData);
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                    return result;

                foreach (var property in doc.RootElement.EnumerateObject())
                {
                    var key = ToLabel(property.Name);
                    var value = property.Value;

                    if (property.NameEquals("title"))
                    {
                        result.Title = JsonValueToString(value);
                        continue;
                    }

                    if (property.NameEquals("notes"))
                    {
                        result.Notes = JsonValueToString(value);
                        continue;
                    }

                    if (property.NameEquals("highlights") || property.NameEquals("keyHighlights"))
                    {
                        if (value.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in value.EnumerateArray().Take(20))
                            {
                                result.BulletHighlights.Add(JsonValueToString(item));
                            }

                            if (value.GetArrayLength() > 20)
                                result.BulletHighlights.Add($"... and {value.GetArrayLength() - 20} more item(s)");
                        }
                        else
                        {
                            result.Highlights.Add(new ReportRow("Highlights", JsonValueToString(value)));
                        }
                        continue;
                    }

                    if (property.NameEquals("autoTaskProgress") && value.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var child in value.EnumerateObject())
                        {
                            result.AutoTaskProgress.Add(new ReportRow(ToLabel(child.Name), JsonValueToString(child.Value)));
                        }
                        continue;
                    }

                    if (property.NameEquals("schemaVersion"))
                    {
                        result.Highlights.Add(new ReportRow("Schema Version", JsonValueToString(value)));
                        continue;
                    }

                    if (IsSimple(value))
                    {
                        result.Highlights.Add(new ReportRow(key, JsonValueToString(value)));
                        continue;
                    }

                    if (value.ValueKind == JsonValueKind.Object)
                    {
                        var section = new ReportSection { Title = key };
                        foreach (var child in value.EnumerateObject())
                        {
                            section.Rows.Add(new ReportRow(ToLabel(child.Name), JsonValueToString(child.Value)));
                        }

                        if (section.Rows.Any())
                            result.Sections.Add(section);

                        continue;
                    }

                    if (value.ValueKind == JsonValueKind.Array)
                    {
                        var section = new ReportSection { Title = key };
                        var items = value.EnumerateArray().Take(20).ToList();
                        var index = 1;

                        foreach (var item in items)
                        {
                            if (item.ValueKind == JsonValueKind.Object)
                            {
                                var text = string.Join("; ", item.EnumerateObject().Select(x => $"{ToLabel(x.Name)}: {JsonValueToString(x.Value)}"));
                                section.Items.Add($"Item {index}: {text}");
                            }
                            else
                            {
                                section.Items.Add(JsonValueToString(item));
                            }
                            index++;
                        }

                        if (value.GetArrayLength() > 20)
                            section.Items.Add($"... and {value.GetArrayLength() - 20} more item(s)");

                        if (section.Items.Any())
                            result.Sections.Add(section);
                    }
                }

                result.Highlights = result.Highlights.Take(8).ToList();
            }
            catch
            {
                return result;
            }

            return result;
        }

        private static bool IsSimple(JsonElement value) =>
            value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null;

        private static string JsonValueToString(JsonElement value)
        {
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? string.Empty,
                JsonValueKind.Number => value.ToString(),
                JsonValueKind.True => "Yes",
                JsonValueKind.False => "No",
                JsonValueKind.Null => "N/A",
                JsonValueKind.Array => $"Array ({value.GetArrayLength()} item(s))",
                JsonValueKind.Object => CompactObject(value),
                _ => value.ToString()
            };
        }

        private static string CompactObject(JsonElement value)
        {
            var pairs = value.EnumerateObject()
                .Take(6)
                .Select(p => $"{ToLabel(p.Name)}: {JsonValueToString(p.Value)}")
                .ToList();

            if (value.EnumerateObject().Count() > 6)
                pairs.Add("...");

            return string.Join("; ", pairs);
        }

        private static string ToLabel(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            var chars = new StringBuilder(raw.Length + 8);
            for (var i = 0; i < raw.Length; i++)
            {
                var ch = raw[i];
                if (ch == '_' || ch == '-')
                {
                    chars.Append(' ');
                    continue;
                }

                if (i > 0 && char.IsUpper(ch) && raw[i - 1] != '_' && raw[i - 1] != '-' && !char.IsUpper(raw[i - 1]))
                    chars.Append(' ');

                chars.Append(ch);
            }

            var normalized = chars.ToString().Trim();
            if (string.IsNullOrWhiteSpace(normalized))
                return string.Empty;

            return char.ToUpperInvariant(normalized[0]) + normalized[1..];
        }

        private sealed class ParsedReportData
        {
            public string? Title { get; set; }
            public string? Notes { get; set; }
            public List<string> BulletHighlights { get; set; } = new();
            public List<ReportRow> AutoTaskProgress { get; set; } = new();
            public List<ReportRow> Highlights { get; set; } = new();
            public List<ReportSection> Sections { get; set; } = new();
        }

        private sealed class ReportSection
        {
            public string Title { get; set; } = string.Empty;
            public List<ReportRow> Rows { get; set; } = new();
            public List<string> Items { get; set; } = new();
        }

        private sealed record ReportRow(string Key, string Value);

        private static string Escape(string? text) => System.Net.WebUtility.HtmlEncode(text ?? string.Empty);
    }
}
