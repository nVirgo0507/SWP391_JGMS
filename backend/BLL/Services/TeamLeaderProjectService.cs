using BLL.DTOs.Admin;
using BLL.DTOs.Jira;
using BLL.Helpers;
using BLL.Services.Interface;
using DAL.Models;
using DAL.Repositories.Interface;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Task = System.Threading.Tasks.Task;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using BLL.Helpers;

namespace BLL.Services
{
    /// <summary>
    /// Service for team leader-scoped operations with group access control
    /// BR-055: Team Leader Group-Scoped Access - Team leaders can only manage their own group's project
    /// Validation: Check user is leader of the group via GROUP_MEMBER.is_leader
    /// Error Message: "Access denied. You are not the leader of this group."
    /// </summary>
    public class TeamLeaderProjectService : ITeamLeaderProjectService
    {
        private readonly IGroupMemberRepository _memberRepository;
        private readonly ILeaderValidationService _leaderValidationService;
        private readonly IStudentGroupRepository _groupRepository;
        private readonly IUserRepository _userRepository;
        private readonly ITaskRepository _taskRepository;
        private readonly IJiraIssueRepository _jiraIssueRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly ICommitRepository _commitRepository;
        private readonly ICommitStatisticRepository _commitStatisticRepository;
        private readonly IRequirementRepository _requirementRepository;
        private readonly IJiraIntegrationRepository _jiraIntegrationRepository;
        private readonly IJiraApiService _jiraApiService;
        private readonly IPersonalTaskStatisticRepository _personalTaskStatisticRepository;
        private readonly byte[] _encryptionKey;
        private readonly ISrsDocumentRepository _srsDocumentRepository;
        private readonly IAiChatService _aiChatService;

        public TeamLeaderProjectService(
            ILeaderValidationService leaderValidationService,
            IGroupMemberRepository memberRepository,
            IStudentGroupRepository groupRepository,
            IUserRepository userRepository,
            ITaskRepository taskRepository,
            IJiraIssueRepository jiraIssueRepository,
            IProjectRepository projectRepository,
            ICommitRepository commitRepository,
            ICommitStatisticRepository commitStatisticRepository,
            IRequirementRepository requirementRepository,
            IJiraIntegrationRepository jiraIntegrationRepository,
            IJiraApiService jiraApiService,
            IPersonalTaskStatisticRepository personalTaskStatisticRepository,
            IConfiguration configuration,
            ISrsDocumentRepository srsDocumentRepository,
            IAiChatService aiChatService)
        {
            _leaderValidationService = leaderValidationService;
            _memberRepository = memberRepository;
            _groupRepository = groupRepository;
            _userRepository = userRepository;
            _taskRepository = taskRepository;
            _jiraIssueRepository = jiraIssueRepository;
            _projectRepository = projectRepository;
            _commitRepository = commitRepository;
            _commitStatisticRepository = commitStatisticRepository;
            _requirementRepository = requirementRepository;
            _jiraIntegrationRepository = jiraIntegrationRepository;
            _jiraApiService = jiraApiService;
            _personalTaskStatisticRepository = personalTaskStatisticRepository;
            _srsDocumentRepository = srsDocumentRepository;
            _aiChatService = aiChatService;
            // Derive the same stable AES-GCM key as JiraIntegrationService
            var jwtKey = configuration["Jwt:Key"] ?? "JGMS_DEFAULT_ENCRYPTION_KEY_32CH";
            _encryptionKey = SHA256.HashData(Encoding.UTF8.GetBytes(jwtKey));
        }

        /// <summary>
        /// BR-055: Get project details for the leader's group
        /// Validates that user is leader of the group
        /// </summary>
        public async Task<ProjectResponseDTO?> GetGroupProjectAsync(int userId, int groupId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
            {
                throw new Exception("Group not found");
            }

            // BR-055: Validate user is leader of the group
            await _leaderValidationService.ValidateLeaderAccessAsync(userId, groupId);

            // Get project from repository by groupId
            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project == null) return null;

            return new ProjectResponseDTO
            {
                ProjectId = project.ProjectId,
                GroupId = project.GroupId,
                GroupCode = project.Group?.GroupCode,
                GroupName = project.Group?.GroupName,
                ProjectName = project.ProjectName,
                Description = project.Description,
                StartDate = project.StartDate,
                EndDate = project.EndDate,
                Status = project.Status?.ToString(),
                CreatedAt = project.CreatedAt,
                UpdatedAt = project.UpdatedAt
            };
        }

        public async Task<List<ProgressReportResponseDTO>> GetGroupProgressReportsAsync(int userId, int groupId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null) throw new Exception("Group not found");

            await _leaderValidationService.ValidateLeaderAccessAsync(userId, groupId);

            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project == null) return new List<ProgressReportResponseDTO>();

            var reports = await _projectRepository.GetProgressReportsByProjectIdAsync(project.ProjectId);
            return reports.Select(MapProgressReportToDTO).ToList();
        }

        public async Task<ProgressReportResponseDTO> CreateProgressReportAsync(int userId, int groupId, CreateProgressReportDTO dto)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null) throw new Exception("Group not found");

            await _leaderValidationService.ValidateLeaderAccessAsync(userId, groupId);

            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project == null) throw new Exception("No project found for this group");

            if (dto.ReportPeriodStart.HasValue && dto.ReportPeriodEnd.HasValue && dto.ReportPeriodStart > dto.ReportPeriodEnd)
                throw new Exception("Report period start must be earlier than or equal to report period end.");

            if (dto.ReportData.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
                throw new Exception("reportData is required and must be valid JSON.");

            if (dto.ReportData.ValueKind is not JsonValueKind.Object)
                throw new Exception("reportData must be a JSON object.");

            var reportDataJson = await BuildReportDataWithTaskProgressAsync(
                project.ProjectId,
                dto.ReportData,
                dto.ReportPeriodStart,
                dto.ReportPeriodEnd);

            var entity = new ProgressReport
            {
                ProjectId = project.ProjectId,
                ReportType = ParseReportType(dto.ReportType),
                ReportPeriodStart = dto.ReportPeriodStart,
                ReportPeriodEnd = dto.ReportPeriodEnd,
                ReportData = reportDataJson,
                Summary = dto.Summary,
                FilePath = dto.FilePath,
                GeneratedBy = userId,
                GeneratedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            await _projectRepository.AddProgressReportAsync(entity);

            var user = await _userRepository.GetByIdAsync(userId);
            var result = MapProgressReportToDTO(entity);
            result.GeneratedByName = user?.FullName;
            return result;
        }

        public async Task<ProgressReportTemplateDTO> GetGroupProgressReportTemplateAsync(int userId, int groupId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null) throw new Exception("Group not found");

            await _leaderValidationService.ValidateLeaderAccessAsync(userId, groupId);

            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project == null) throw new Exception("No project found for this group");

            return new ProgressReportTemplateDTO
            {
                SchemaVersion = "1.0",
                AllowedReportTypes = new List<string> { "weekly", "sprint", "task_assignment", "task_completion" },
                Fields = new List<ProgressReportTemplateFieldDTO>
                {
                    new() { Key = "title", Label = "Report Title", InputType = "text", Required = true, Placeholder = "Weekly progress report", Description = "Main heading shown in exported Word/PDF" },
                    new() { Key = "notes", Label = "Notes", InputType = "textarea", Required = false, Placeholder = "Main API endpoints done, testing in progress", Description = "Free text summary for current reporting period" },
                    new() { Key = "keyHighlights", Label = "Key Highlights", InputType = "string_array", Required = false, Placeholder = "One highlight per line", Description = "Bullet points displayed in exports" },
                    new() { Key = "autoTaskProgress", Label = "Task Progress", InputType = "object", Required = false, Description = "Auto-filled by backend when creating report" },
                    new() { Key = "autoCommitStatistics", Label = "GitHub Commit Statistics", InputType = "object", Required = false, Description = "Auto-filled by backend based on report period" }
                },
                ReportDataTemplate = new
                {
                    schemaVersion = "1.0",
                    title = $"{project.ProjectName} progress report",
                    notes = string.Empty,
                    keyHighlights = new[] { "", "" },
                    autoTaskProgress = new
                    {
                        done = 0,
                        inProgress = 0,
                        todo = 0,
                        total = 0,
                        completionRate = 0,
                        basedOn = "assigned_tasks",
                        generatedAtUtc = DateTime.UtcNow.ToString("O")
                    },
                    autoCommitStatistics = new
                    {
                        basedOn = "report_period",
                        commitCount = 0,
                        contributors = 0,
                        totalAdditions = 0,
                        totalDeletions = 0,
                        totalChangedFiles = 0,
                        firstCommitAtUtc = (string?)null,
                        lastCommitAtUtc = (string?)null,
                        generatedAtUtc = DateTime.UtcNow.ToString("O")
                    }
                },
                AutoGeneratedFields = new List<string>
                {
                    "autoTaskProgress.done",
                    "autoTaskProgress.inProgress",
                    "autoTaskProgress.todo",
                    "autoTaskProgress.total",
                    "autoTaskProgress.completionRate",
                    "autoTaskProgress.generatedAtUtc",
                    "autoCommitStatistics.basedOn",
                    "autoCommitStatistics.commitCount",
                    "autoCommitStatistics.contributors",
                    "autoCommitStatistics.totalAdditions",
                    "autoCommitStatistics.totalDeletions",
                    "autoCommitStatistics.totalChangedFiles",
                    "autoCommitStatistics.firstCommitAtUtc",
                    "autoCommitStatistics.lastCommitAtUtc",
                    "autoCommitStatistics.generatedAtUtc"
                }
            };
        }

        public async Task<(byte[] content, string fileName, string contentType)> ExportGroupProgressReportAsync(
            int userId,
            int groupId,
            int reportId,
            string format)
        {
            var group = await _groupRepository.GetByIdAsync(groupId)
                ?? throw new Exception("Group not found");

            await _leaderValidationService.ValidateLeaderAccessAsync(userId, groupId);

            var project = await _projectRepository.GetByGroupIdAsync(groupId)
                ?? throw new Exception("Project not found for this group.");

            var report = (await _projectRepository.GetProgressReportsByProjectIdAsync(project.ProjectId))
                .FirstOrDefault(r => r.ReportId == reportId);

            if (report == null)
                throw new Exception("Progress report not found");

            var normalizedFormat = (format ?? string.Empty).Trim().ToLowerInvariant();
            var safeGroupCode = TeamLeaderHelper.SanitizeFileToken(group.GroupCode ?? $"group_{groupId}");
            var baseFileName = $"progress_report_{safeGroupCode}_{report.ReportId}_{report.GeneratedAt:yyyyMMdd_HHmmss}";

            if (normalizedFormat is "word" or "doc" or "docx")
            {
                var wordHtml = ProgressReportExportHelper.GenerateProgressReportWordHtml(group, project, report);
                return (Encoding.UTF8.GetBytes(wordHtml), $"{baseFileName}.doc", "application/msword");
            }

            if (normalizedFormat == "pdf")
            {
                QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
                var pdfBytes = ProgressReportExportHelper.GenerateProgressReportPdf(group, project, report);
                return (pdfBytes, $"{baseFileName}.pdf", "application/pdf");
            }

            throw new Exception("Invalid format. Supported formats: word, pdf");
        }

        public async Task<GroupCommitStatisticsResponseDTO> GetGroupCommitStatisticsAsync(int userId, int groupId, DateOnly? startDate = null, DateOnly? endDate = null)
        {
            var group = await _groupRepository.GetByIdAsync(groupId)
                ?? throw new Exception("Group not found");

            await _leaderValidationService.ValidateLeaderAccessAsync(userId, groupId);

            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project == null)
                return new GroupCommitStatisticsResponseDTO { GroupId = groupId, GroupCode = group.GroupCode };

            // Fetch all commits to calculate overall total
            var commits = await _commitRepository.GetCommitsByProjectIdAsync(project.ProjectId);
            var overallCommits = commits.Count;

            // Filter commits if dates are provided
            var filteredCommits = commits;
            if (startDate.HasValue || endDate.HasValue)
            {
                filteredCommits = commits.Where(c =>
                {
                    var commitDate = DateOnly.FromDateTime(c.CommitDate);
                    return (!startDate.HasValue || commitDate >= startDate.Value) &&
                           (!endDate.HasValue || commitDate <= endDate.Value);
                }).ToList();
            }

            var lastCommitByUser = commits
                .GroupBy(c => c.UserId)
                .ToDictionary(g => g.Key, g => g.Max(x => x.CommitDate));

            // Fetch all members of the group to ensure everyone is listed
            var groupMembers = await _memberRepository.GetByGroupIdAsync(groupId);

            var byUser = filteredCommits.GroupBy(c => c.UserId).ToDictionary(g => g.Key, g => g.ToList());
            var periodStart = startDate ?? (filteredCommits.Any() ? DateOnly.FromDateTime(filteredCommits.Min(c => c.CommitDate).Date) : null);
            var periodEnd = endDate ?? (filteredCommits.Any() ? DateOnly.FromDateTime(filteredCommits.Max(c => c.CommitDate).Date) : null);

            var memberStats = groupMembers.Select(gm =>
            {
                var userId = gm.UserId;
                var uc = byUser.TryGetValue(userId, out var list) ? list : new List<Commit>();
                
                var totalCommits = uc.Count;
                decimal commitFrequency = 0;
                int avgCommitSize = 0;

                if (totalCommits > 0)
                {
                    var firstDate = uc.Min(c => c.CommitDate).Date;
                    var lastDate  = uc.Max(c => c.CommitDate).Date;
                    var days = Math.Max(1, (lastDate - firstDate).TotalDays + 1);
                    commitFrequency = Math.Round((decimal)totalCommits / (decimal)days, 2);
                    avgCommitSize = (int)Math.Round(uc.Average(c => (c.Additions ?? 0) + (c.Deletions ?? 0)));
                }

                return new MemberCommitStatisticsDTO
                {
                    UserId = userId,
                    UserName = gm.User?.FullName ?? $"User {userId}",
                    TotalCommits = totalCommits,
                    TotalAdditions = uc.Sum(c => c.Additions ?? 0),
                    TotalDeletions = uc.Sum(c => c.Deletions ?? 0),
                    TotalChangedFiles = uc.Sum(c => c.ChangedFiles ?? 0),
                    CommitFrequency = commitFrequency,
                    AvgCommitSize = avgCommitSize,
                    LastCommitDate = lastCommitByUser.TryGetValue(userId, out var lcd) ? lcd : (DateTime?)null
                };
            }).OrderByDescending(m => m.TotalCommits).ToList();

            return new GroupCommitStatisticsResponseDTO
            {
                GroupId = groupId,
                GroupCode = group.GroupCode,
                ProjectId = project.ProjectId,
                OverallCommits = overallCommits,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                TotalCommits = memberStats.Sum(m => m.TotalCommits),
                TotalAdditions = memberStats.Sum(m => m.TotalAdditions),
                TotalDeletions = memberStats.Sum(m => m.TotalDeletions),
                TotalChangedFiles = memberStats.Sum(m => m.TotalChangedFiles),
                Members = memberStats
            };
        }

        private async Task<string> BuildReportDataWithTaskProgressAsync(
            int projectId,
            JsonElement reportData,
            DateOnly? reportPeriodStart,
            DateOnly? reportPeriodEnd)
        {
            var root = JsonNode.Parse(reportData.GetRawText()) as JsonObject
                ?? throw new Exception("reportData must be a valid JSON object.");

            var tasks = await _taskRepository.GetTasksByProjectIdAsync(projectId);
            var assignedTasks = tasks.Where(t => t.AssignedTo.HasValue).ToList();
            var source = assignedTasks.Any() ? assignedTasks : tasks;

            var todoCount = source.Count(t => t.Status == DAL.Models.TaskStatus.todo);
            var inProgressCount = source.Count(t => t.Status == DAL.Models.TaskStatus.in_progress);
            var doneCount = source.Count(t => t.Status == DAL.Models.TaskStatus.done);
            var totalCount = source.Count;
            var completionRate = totalCount == 0
                ? 0
                : (int)Math.Round((double)doneCount * 100 / totalCount, MidpointRounding.AwayFromZero);

            root["autoTaskProgress"] = new JsonObject
            {
                ["basedOn"] = assignedTasks.Any() ? "assigned_tasks" : "all_project_tasks",
                ["total"] = totalCount,
                ["todo"] = todoCount,
                ["inProgress"] = inProgressCount,
                ["done"] = doneCount,
                ["completionRate"] = completionRate,
                ["generatedAtUtc"] = DateTime.UtcNow
            };

            var commits = await _commitRepository.GetCommitsByProjectIdAsync(projectId);

            var filteredCommits = commits.Where(c =>
            {
                var commitDate = DateOnly.FromDateTime(c.CommitDate);
                if (reportPeriodStart.HasValue && commitDate < reportPeriodStart.Value)
                    return false;
                if (reportPeriodEnd.HasValue && commitDate > reportPeriodEnd.Value)
                    return false;
                return true;
            }).ToList();

            var hasDateFilter = reportPeriodStart.HasValue || reportPeriodEnd.HasValue;

            root["autoCommitStatistics"] = new JsonObject
            {
                ["basedOn"] = hasDateFilter ? "report_period" : "all_project_commits",
                ["periodStart"] = reportPeriodStart?.ToString("yyyy-MM-dd"),
                ["periodEnd"] = reportPeriodEnd?.ToString("yyyy-MM-dd"),
                ["commitCount"] = filteredCommits.Count,
                ["contributors"] = filteredCommits.Select(c => c.UserId).Distinct().Count(),
                ["totalAdditions"] = filteredCommits.Sum(c => c.Additions ?? 0),
                ["totalDeletions"] = filteredCommits.Sum(c => c.Deletions ?? 0),
                ["totalChangedFiles"] = filteredCommits.Sum(c => c.ChangedFiles ?? 0),
                ["firstCommitAtUtc"] = filteredCommits.Any() ? filteredCommits.Min(c => c.CommitDate).ToString("O") : null,
                ["lastCommitAtUtc"] = filteredCommits.Any() ? filteredCommits.Max(c => c.CommitDate).ToString("O") : null,
                ["generatedAtUtc"] = DateTime.UtcNow
            };

            return root.ToJsonString();
        }

        private static ReportType ParseReportType(string value)
        {
            if (Enum.TryParse<ReportType>(value?.Trim(), true, out var parsed))
                return parsed;

            throw new Exception("Invalid reportType. Allowed values: task_assignment, task_completion, weekly, sprint.");
        }

        private static ProgressReportResponseDTO MapProgressReportToDTO(ProgressReport report)
        {
            return new ProgressReportResponseDTO
            {
                ReportId = report.ReportId,
                ProjectId = report.ProjectId,
                ReportType = report.ReportType.ToString(),
                ReportPeriodStart = report.ReportPeriodStart,
                ReportPeriodEnd = report.ReportPeriodEnd,
                ReportData = report.ReportData,
                Summary = report.Summary,
                FilePath = report.FilePath,
                GeneratedBy = report.GeneratedBy,
                GeneratedByName = report.GeneratedByNavigation?.FullName,
                GeneratedAt = report.GeneratedAt,
                CreatedAt = report.CreatedAt
            };
        }
    }
}
