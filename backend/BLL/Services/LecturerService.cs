using BLL.DTOs.Admin;
using AdminDTOs = BLL.DTOs.Admin;
using BLL.Services.Interface;
using DAL.Models;
using DAL.Repositories.Interface;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Task = System.Threading.Tasks.Task;

namespace BLL.Services
{
    public class LecturerService : ILecturerService
    {
        private readonly IStudentGroupRepository _groupRepository;
        private readonly IUserRepository _userRepository;
        private readonly IGroupMemberRepository _memberRepository;
        private readonly IRequirementRepository _requirementRepository;
        private readonly ITaskRepository _taskRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly ICommitRepository _commitRepository;
        private readonly IJiraIntegrationRepository _jiraIntegrationRepository;
        private readonly IGithubIntegrationRepository _githubIntegrationRepository;
        private readonly IJiraIssueRepository _jiraIssueRepository;
        private readonly IGithubCommitRepository _githubCommitRepository;
        private readonly ICommitStatisticRepository _commitStatisticRepository;

        public LecturerService(
            IStudentGroupRepository groupRepository,
            IUserRepository userRepository,
            IGroupMemberRepository memberRepository,
            IRequirementRepository requirementRepository,
            ITaskRepository taskRepository,
            IProjectRepository projectRepository,
            ICommitRepository commitRepository,
            IJiraIntegrationRepository jiraIntegrationRepository,
            IGithubIntegrationRepository githubIntegrationRepository,
            IJiraIssueRepository jiraIssueRepository,
            IGithubCommitRepository githubCommitRepository,
            ICommitStatisticRepository commitStatisticRepository)
        {
            _groupRepository = groupRepository;
            _userRepository = userRepository;
            _memberRepository = memberRepository;
            _requirementRepository = requirementRepository;
            _taskRepository = taskRepository;
            _projectRepository = projectRepository;
            _commitRepository = commitRepository;
            _jiraIntegrationRepository = jiraIntegrationRepository;
            _githubIntegrationRepository = githubIntegrationRepository;
            _jiraIssueRepository = jiraIssueRepository;
            _githubCommitRepository = githubCommitRepository;
            _commitStatisticRepository = commitStatisticRepository;
        }

        public async Task<AdminDTOs.UserResponseDTO> GetMyProfileAsync(int lecturerId)
        {
            var user = await _userRepository.GetByIdAsync(lecturerId);

            if (user == null || user.Role != UserRole.lecturer)
            {
                throw new Exception("Lecturer not found");
            }

            return new AdminDTOs.UserResponseDTO
            {
                UserId = user.UserId,
                Email = user.Email,
                FullName = user.FullName,
                Role = user.Role,
                Phone = user.Phone,
                Status = user.Status,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };
        }

        private async Task CheckLecturerAssignmentAsync(int userId, StudentGroup group)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user != null && user.Role == UserRole.admin) return;

            if (group.LecturerId != userId)
            {
                throw new Exception("Access denied. You are not assigned to this group.");
            }
        }

        public async Task<StudentGroupResponseDTO?> GetGroupByIdAsync(int lecturerId, int groupId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
            {
                throw new Exception("Student group not found");
            }

            await CheckLecturerAssignmentAsync(lecturerId, group);

            var groupDetails = await _groupRepository.GetGroupWithDetailsAsync(groupId);
            if (groupDetails == null) return null;

            var dto = MapToGroupResponse(groupDetails);

            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project != null)
            {
                dto.Project = await MapToProjectResponseAsync(project);
            }

            return dto;
        }

        public async Task<List<StudentGroupResponseDTO>> GetMyGroupsAsync(int lecturerId)
        {
            var lecturer = await _userRepository.GetByIdAsync(lecturerId);
            if (lecturer == null || (lecturer.Role != UserRole.lecturer && lecturer.Role != UserRole.admin))
            {
                throw new Exception("User not found or invalid role for this operation");
            }

            var groups = await _groupRepository.GetByLecturerIdAsync(lecturerId);
            var dtos = new List<StudentGroupResponseDTO>();

            foreach (var group in groups)
            {
                var dto = MapToGroupResponse(group);
                var project = await _projectRepository.GetByGroupIdAsync(group.GroupId);
                if (project != null)
                {
                    dto.Project = await MapToProjectResponseAsync(project);
                }
                dtos.Add(dto);
            }

            return dtos;
        }

        public async Task<List<GroupMemberResponseDTO>> GetGroupMembersAsync(int lecturerId, int groupId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
            {
                throw new Exception("Student group not found");
            }

            await CheckLecturerAssignmentAsync(lecturerId, group);

            var members = await _memberRepository.GetByGroupIdAsync(groupId);
            return members.Select(m => new GroupMemberResponseDTO
            {
                MemberId = m.MembershipId,
                GroupId = m.GroupId,
                UserId = m.UserId,
                UserName = m.User.FullName,
                Email = m.User.Email,
                IsLeader = m.IsLeader.GetValueOrDefault(false),
                JoinedAt = m.JoinedAt,
                LeftAt = m.LeftAt
            }).ToList();
        }

        public async Task<BulkAddResult> AddStudentsToGroupAsync(int lecturerId, int groupId, List<string> studentIdentifiers)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
                throw new Exception("Student group not found");

            await CheckLecturerAssignmentAsync(lecturerId, group);

            var result = new BulkAddResult();

            foreach (var identifier in studentIdentifiers)
            {
                try
                {
                    // Resolve identifier → student ID
                    User? student = null;
                    if (int.TryParse(identifier, out var numId))
                        student = await _userRepository.GetByIdAsync(numId);
                    else
                        student = await _userRepository.GetByEmailAsync(identifier);

                    if (student == null)
                        throw new Exception($"User '{identifier}' not found.");
                    if (student.Role != UserRole.student)
                        throw new Exception($"'{identifier}' is not a student account.");

                    if (await _memberRepository.IsMemberOfGroupAsync(groupId, student.UserId))
                        throw new Exception("Already a member of this group.");

                    if (await _memberRepository.IsStudentInAnyGroupAsync(student.UserId))
                        throw new Exception("Already a member of another group.");

                    var previous = await _memberRepository.GetPreviousMembershipAsync(groupId, student.UserId);
                    if (previous != null)
                        await _memberRepository.RejoinAsync(previous);
                    else
                        await _memberRepository.AddAsync(new GroupMember
                        {
                            GroupId = groupId,
                            UserId = student.UserId,
                            IsLeader = false,
                            JoinedAt = DateTime.UtcNow
                        });

                    result.Added.Add(identifier);
                    result.SuccessCount++;
                }
                catch (Exception ex)
                {
                    result.Failures.Add(new BulkAddFailure { Identifier = identifier, Reason = ex.Message });
                    result.FailureCount++;
                }
            }

            return result;
        }

        public async System.Threading.Tasks.Task RemoveStudentFromGroupAsync(int lecturerId, int groupId, string studentIdentifier)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
                throw new Exception("Student group not found");

            await CheckLecturerAssignmentAsync(lecturerId, group);

            User? student = null;
            if (int.TryParse(studentIdentifier, out var numId))
                student = await _userRepository.GetByIdAsync(numId);
            else
                student = await _userRepository.GetByEmailAsync(studentIdentifier);

            if (student == null)
                throw new KeyNotFoundException($"Student '{studentIdentifier}' not found.");

            if (!await _memberRepository.IsMemberOfGroupAsync(groupId, student.UserId))
                throw new Exception("Student is not a member of this group");

            if (group.LeaderId == student.UserId)
                throw new Exception("Cannot remove the group leader. Assign a different leader first before removing this student.");

            await _memberRepository.RemoveAsync(groupId, student.UserId);
        }

        public async Task<StudentGroupResponseDTO> UpdateGroupAsync(int lecturerId, int groupId, UpdateStudentGroupDTO dto)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
            {
                throw new Exception("Student group not found");
            }

            await CheckLecturerAssignmentAsync(lecturerId, group);

            if (!string.IsNullOrEmpty(dto.GroupName))
                group.GroupName = dto.GroupName;

            if (dto.Status.HasValue)
                group.Status = dto.Status.Value;

            group.UpdatedAt = DateTime.UtcNow;
            await _groupRepository.UpdateAsync(group);

            var updatedGroup = await _groupRepository.GetGroupWithDetailsAsync(groupId);
            var resDto = MapToGroupResponse(updatedGroup!);

            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project != null)
            {
                resDto.Project = await MapToProjectResponseAsync(project);
            }

            return resDto;
        }

        private async Task<ProjectResponseDTO> MapToProjectResponseAsync(Project project)
        {
            var dto = new ProjectResponseDTO
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

            // Enhanced with integration status
            var jira = await _jiraIntegrationRepository.GetByProjectIdAsync(project.ProjectId);
            if (jira != null)
            {
                dto.JiraStatus = new ProjectIntegrationStatusDTO
                {
                    IsConfigured = true,
                    SyncStatus = jira.SyncStatus.ToString(),
                    LastSync = jira.LastSync,
                    TotalItems = await _jiraIssueRepository.GetCountByProjectIdAsync(project.ProjectId)
                };
            }

            var github = await _githubIntegrationRepository.GetByProjectIdAsync(project.ProjectId);
            if (github != null)
            {
                dto.GithubStatus = new ProjectIntegrationStatusDTO
                {
                    IsConfigured = true,
                    SyncStatus = github.SyncStatus.ToString(),
                    LastSync = github.LastSync,
                    TotalItems = await _githubCommitRepository.GetCountByProjectIdAsync(project.ProjectId)
                };
            }

            return dto;
        }

        public async Task<List<RequirementResponseDTO>> GetGroupRequirementsAsync(int lecturerId, int groupId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId)
                ?? throw new Exception("Student group not found");

            await CheckLecturerAssignmentAsync(lecturerId, group);

            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project == null)
                return new List<RequirementResponseDTO>();

            var requirements = await _requirementRepository.GetByProjectIdAsync(project.ProjectId);
            return requirements.Select(MapToRequirementResponse).ToList();
        }

        public async Task<List<TaskResponseDTO>> GetGroupTasksAsync(int lecturerId, int groupId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId)
                ?? throw new Exception("Student group not found");

            await CheckLecturerAssignmentAsync(lecturerId, group);

            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project == null)
                return new List<TaskResponseDTO>();

            var tasks = await _taskRepository.GetTasksByProjectIdAsync(project.ProjectId);
            return tasks.Select(MapToTaskResponse).ToList();
        }

        public async Task<List<ProgressReportResponseDTO>> GetProjectProgressReportsAsync(int lecturerId, int groupId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId)
                ?? throw new Exception("Student group not found");

            await CheckLecturerAssignmentAsync(lecturerId, group);

            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project == null)
                return new List<ProgressReportResponseDTO>();

            var reports = await _projectRepository.GetProgressReportsByProjectIdAsync(project.ProjectId);
            return reports.Select(r => new ProgressReportResponseDTO
            {
                ReportId = r.ReportId,
                ProjectId = r.ProjectId,
                ReportType = r.ReportType.ToString(),
                ReportPeriodStart = r.ReportPeriodStart,
                ReportPeriodEnd = r.ReportPeriodEnd,
                ReportData = r.ReportData,
                Summary = r.Summary,
                FilePath = r.FilePath,
                GeneratedBy = r.GeneratedBy,
                GeneratedByName = r.GeneratedByNavigation?.FullName,
                GeneratedAt = r.GeneratedAt,
                CreatedAt = r.CreatedAt
            }).ToList();
        }

        public async Task<(byte[] content, string fileName, string contentType)> ExportProjectProgressReportAsync(
            int lecturerId,
            int groupId,
            int reportId,
            string format)
        {
            var group = await _groupRepository.GetByIdAsync(groupId)
                ?? throw new Exception("Student group not found");

            if (group.LecturerId != lecturerId)
                throw new Exception("Access denied. You are not assigned to this group.");

            var project = await _projectRepository.GetByGroupIdAsync(groupId)
                ?? throw new Exception("Project not found for this group.");

            var report = (await _projectRepository.GetProgressReportsByProjectIdAsync(project.ProjectId))
                .FirstOrDefault(r => r.ReportId == reportId);

            if (report == null)
                throw new Exception("Progress report not found");

            var normalizedFormat = (format ?? string.Empty).Trim().ToLowerInvariant();
            var safeGroupCode = SanitizeFileToken(group.GroupCode ?? $"group_{groupId}");
            var baseFileName = $"progress_report_{safeGroupCode}_{report.ReportId}_{report.GeneratedAt:yyyyMMdd_HHmmss}";

            if (normalizedFormat is "word" or "doc" or "docx")
            {
                var wordHtml = GenerateProgressReportWordHtml(group, project, report);
                return (Encoding.UTF8.GetBytes(wordHtml), $"{baseFileName}.doc", "application/msword");
            }

            if (normalizedFormat == "pdf")
            {
                QuestPDF.Settings.License = LicenseType.Community;
                var pdfBytes = GenerateProgressReportPdf(group, project, report);
                return (pdfBytes, $"{baseFileName}.pdf", "application/pdf");
            }

            throw new Exception("Invalid format. Supported formats: word, pdf");
        }

        public async Task<GroupCommitStatisticsResponseDTO> GetGithubCommitStatisticsAsync(int lecturerId, int groupId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId)
                ?? throw new Exception("Student group not found");

            await CheckLecturerAssignmentAsync(lecturerId, group);

            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project == null)
            {
                return new GroupCommitStatisticsResponseDTO
                {
                    GroupId = groupId,
                    GroupCode = group.GroupCode
                };
            }

            await _commitStatisticRepository.RecalculateProjectStatisticsAsync(project.ProjectId);
            var statsRows = await _commitStatisticRepository.GetLatestByProjectIdAsync(project.ProjectId);
            if (statsRows.Any())
            {
                var latestByUser = statsRows;

                return new GroupCommitStatisticsResponseDTO
                {
                    GroupId = groupId,
                    GroupCode = group.GroupCode,
                    ProjectId = project.ProjectId,
                    PeriodStart = latestByUser.Min(s => s.PeriodStart),
                    PeriodEnd = latestByUser.Max(s => s.PeriodEnd),
                    TotalCommits = latestByUser.Sum(s => s.TotalCommits ?? 0),
                    TotalAdditions = latestByUser.Sum(s => s.TotalAdditions ?? 0),
                    TotalDeletions = latestByUser.Sum(s => s.TotalDeletions ?? 0),
                    TotalChangedFiles = latestByUser.Sum(s => s.TotalChangedFiles ?? 0),
                    Members = latestByUser
                        .OrderByDescending(s => s.TotalCommits ?? 0)
                        .Select(s => new MemberCommitStatisticsDTO
                        {
                            UserId = s.UserId,
                            UserName = s.User?.FullName ?? $"User {s.UserId}",
                            TotalCommits = s.TotalCommits ?? 0,
                            TotalAdditions = s.TotalAdditions ?? 0,
                            TotalDeletions = s.TotalDeletions ?? 0,
                            TotalChangedFiles = s.TotalChangedFiles ?? 0,
                            CommitFrequency = s.CommitFrequency,
                            AvgCommitSize = s.AvgCommitSize,
                            LastCommitDate = null
                        })
                        .ToList()
                };
            }

            var commits = await _commitRepository.GetCommitsByProjectIdAsync(project.ProjectId);
            if (!commits.Any())
            {
                return new GroupCommitStatisticsResponseDTO
                {
                    GroupId = groupId,
                    GroupCode = group.GroupCode,
                    ProjectId = project.ProjectId
                };
            }

            var byUser = commits.GroupBy(c => c.UserId).ToList();
            var periodStart = DateOnly.FromDateTime(commits.Min(c => c.CommitDate).Date);
            var periodEnd = DateOnly.FromDateTime(commits.Max(c => c.CommitDate).Date);

            var memberStats = byUser
                .Select(g =>
                {
                    var userCommits = g.ToList();
                    var firstDate = userCommits.Min(c => c.CommitDate).Date;
                    var lastDate = userCommits.Max(c => c.CommitDate).Date;
                    var days = Math.Max(1, (lastDate - firstDate).TotalDays + 1);

                    return new MemberCommitStatisticsDTO
                    {
                        UserId = g.Key,
                        UserName = userCommits.First().User?.FullName ?? $"User {g.Key}",
                        TotalCommits = userCommits.Count,
                        TotalAdditions = userCommits.Sum(c => c.Additions ?? 0),
                        TotalDeletions = userCommits.Sum(c => c.Deletions ?? 0),
                        TotalChangedFiles = userCommits.Sum(c => c.ChangedFiles ?? 0),
                        CommitFrequency = Math.Round((decimal)userCommits.Count / (decimal)days, 2),
                        AvgCommitSize = userCommits.Any()
                            ? (int)Math.Round(userCommits.Average(c => (c.Additions ?? 0) + (c.Deletions ?? 0)))
                            : 0,
                        LastCommitDate = userCommits.Max(c => c.CommitDate)
                    };
                })
                .OrderByDescending(m => m.TotalCommits)
                .ToList();

            return new GroupCommitStatisticsResponseDTO
            {
                GroupId = groupId,
                GroupCode = group.GroupCode,
                ProjectId = project.ProjectId,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                TotalCommits = commits.Count,
                TotalAdditions = commits.Sum(c => c.Additions ?? 0),
                TotalDeletions = commits.Sum(c => c.Deletions ?? 0),
                TotalChangedFiles = commits.Sum(c => c.ChangedFiles ?? 0),
                Members = memberStats
            };
        }

        private StudentGroupResponseDTO MapToGroupResponse(StudentGroup group)
        {
            var activeMembers = group.GroupMembers
                .Where(m => m.LeftAt == null)
                .OrderByDescending(m => m.IsLeader)
                .ThenBy(m => m.User.FullName)
                .Select(m => new GroupMemberResponseDTO
                {
                    MemberId = m.MembershipId,
                    GroupId  = m.GroupId,
                    UserId   = m.UserId,
                    UserName = m.User.FullName,
                    Email    = m.User.Email,
                    IsLeader = m.IsLeader.GetValueOrDefault(false),
                    JoinedAt = m.JoinedAt,
                    LeftAt   = m.LeftAt
                })
                .ToList();

            return new StudentGroupResponseDTO
            {
                GroupId      = group.GroupId,
                GroupCode    = group.GroupCode,
                GroupName    = group.GroupName,
                LecturerId   = group.LecturerId,
                LecturerName = group.Lecturer?.FullName ?? "Unknown",
                LeaderId     = group.LeaderId,
                LeaderName   = group.Leader?.FullName,
                ProjectId    = group.Project?.ProjectId,
                ProjectName  = group.Project?.ProjectName,
                Status       = group.Status,
                MemberCount  = activeMembers.Count,
                Members      = activeMembers,
                CreatedAt    = group.CreatedAt,
                UpdatedAt    = group.UpdatedAt
            };
        }

        private static RequirementResponseDTO MapToRequirementResponse(Requirement r)
        {
            return new RequirementResponseDTO
            {
                RequirementId = r.RequirementId,
                ProjectId = r.ProjectId,
                JiraIssueId = r.JiraIssueId,
                JiraIssueKey = r.JiraIssue?.IssueKey,
                RequirementCode = r.RequirementCode,
                Title = r.Title,
                Description = r.Description,
                RequirementType = r.RequirementType.ToString(),
                IssueType = r.JiraIssue?.IssueType,
                Priority = r.Priority.ToString(),
                JiraStatus = r.JiraIssue?.Status,
                CreatedBy = r.CreatedBy,
                CreatedByName = r.CreatedByNavigation?.FullName,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            };
        }

        private static TaskResponseDTO MapToTaskResponse(DAL.Models.Task t)
        {
            return new TaskResponseDTO
            {
                TaskId = t.TaskId,
                RequirementId = t.RequirementId,
                RequirementCode = t.Requirement?.RequirementCode,
                JiraIssueId = t.JiraIssueId,
                JiraIssueKey = t.JiraIssue?.IssueKey,
                AssignedTo = t.AssignedTo,
                AssignedToName = t.AssignedToNavigation?.FullName,
                Title = t.Title,
                Description = t.Description,
                Status = t.Status.ToString(),
                Priority = t.Priority.ToString(),
                DueDate = t.DueDate,
                CompletedAt = t.CompletedAt,
                SprintId = t.JiraIssue?.SprintId,
                SprintName = t.JiraIssue?.SprintName,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            };
        }

        private static byte[] GenerateProgressReportPdf(StudentGroup group, Project project, ProgressReport report)
        {
            var parsed = ParseReportData(report.ReportData);
            var prettyReportData = PrettyPrintJson(report.ReportData);

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

                        column.Item().Text("Raw Report Data (Appendix)").Bold().FontSize(12);
                        column.Item()
                            .Border(1)
                            .Padding(8)
                            .Text(prettyReportData)
                            .FontSize(9)
                            .FontFamily("Courier New");
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

        private static string GenerateProgressReportWordHtml(StudentGroup group, Project project, ProgressReport report)
        {
            var parsed = ParseReportData(report.ReportData);
            var prettyReportData = PrettyPrintJson(report.ReportData);
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
            sb.AppendLine("pre { background: #f6f8fa; border: 1px solid #d0d7de; padding: 10px; white-space: pre-wrap; }");
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

            sb.AppendLine("<h2>Raw Report Data (Appendix)</h2>");
            sb.AppendLine($"<pre>{Escape(prettyReportData)}</pre>");
            sb.AppendLine("</body></html>");

            return sb.ToString();
        }

        private static string PrettyPrintJson(string reportData)
        {
            if (string.IsNullOrWhiteSpace(reportData))
                return "{}";

            try
            {
                using var doc = JsonDocument.Parse(reportData);
                return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
            }
            catch
            {
                return reportData;
            }
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

        private static string SanitizeFileToken(string value)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
            return string.IsNullOrWhiteSpace(sanitized) ? "report" : sanitized.Replace(' ', '_');
        }
    }
}
