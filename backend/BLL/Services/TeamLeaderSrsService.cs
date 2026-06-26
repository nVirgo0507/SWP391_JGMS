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
    public class TeamLeaderSrsService : ITeamLeaderSrsService
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

        public TeamLeaderSrsService(
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
        /// Get all SRS documents for the leader's group project
        /// </summary>
        public async Task<List<SrsDocumentResponseDTO>> GetGroupSrsDocumentsAsync(int userId, int groupId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null) throw new Exception("Group not found");

            await _leaderValidationService.ValidateLeaderAccessAsync(userId, groupId);

            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project == null) return new List<SrsDocumentResponseDTO>();

            var documents = await _srsDocumentRepository.GetByProjectIdAsync(project.ProjectId);
            return documents.Select(MapSrsToDTO).ToList();
        }

        /// <summary>
        /// Get a single SRS document by ID with included requirements
        /// </summary>
        public async Task<SrsDocumentResponseDTO?> GetGroupSrsDocumentAsync(int userId, int groupId, int documentId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null) throw new Exception("Group not found");

            await _leaderValidationService.ValidateLeaderAccessAsync(userId, groupId);

            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project == null) return null;

            var doc = await _srsDocumentRepository.GetByIdAsync(documentId);
            if (doc == null || doc.ProjectId != project.ProjectId) return null;

            return MapSrsToDTO(doc);
        }

        /// <summary>
        /// Generate an SRS document from existing requirements.
        /// If ImportFromJira is true, auto-creates requirements from synced Jira issues first.
        /// Snapshots requirement data into SRS_INCLUDED_REQUIREMENT rows for traceability.
        /// </summary>
        public async Task<SrsDocumentResponseDTO> GenerateSrsDocumentAsync(int userId, int groupId, CreateSrsDocumentDTO dto)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null) throw new Exception("Group not found");

            await _leaderValidationService.ValidateLeaderAccessAsync(userId, groupId);

            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project == null) throw new Exception("No project found for this group");

            // Reject duplicate versions for the same project
            if (await _srsDocumentRepository.ExistsByVersionAsync(project.ProjectId, dto.Version))
                throw new Exception($"An SRS document with version \"{dto.Version}\" already exists for this project. Use a different version number.");

            // Auto-import requirements from synced Jira issues
            if (dto.ImportFromJira)
            {
                var jiraIssues = await _jiraIssueRepository.GetByProjectIdAsync(project.ProjectId);

                foreach (var issue in jiraIssues)
                {
                    var reqType = TeamLeaderHelper.ClassifyRequirementType(issue.IssueType, issue.Summary, issue.Description);
                    var priority = issue.Priority switch
                    {
                        JiraPriority.highest or JiraPriority.high => PriorityLevel.high,
                        JiraPriority.low or JiraPriority.lowest => PriorityLevel.low,
                        _ => PriorityLevel.medium
                    };

                    var existing = await _requirementRepository.GetByJiraIssueIdAsync(issue.JiraIssueId);
                    if (existing != null)
                    {
                        // Re-classify existing requirement — corrects stale types from before keyword analysis was added
                        if (existing.RequirementType != reqType)
                        {
                            existing.RequirementType = reqType;
                            await _requirementRepository.UpdateAsync(existing);
                        }
                        continue;
                    }

                    var requirement = new Requirement
                    {
                        ProjectId = project.ProjectId,
                        RequirementCode = issue.IssueKey,
                        Title = issue.Summary,
                        Description = issue.Description,
                        JiraIssueId = issue.JiraIssueId,
                        RequirementType = reqType,
                        Priority = priority,
                        CreatedBy = userId
                    };

                    await _requirementRepository.AddAsync(requirement);
                }
            }

            // Get requirements to include
            var allRequirements = await _requirementRepository.GetByProjectIdAsync(project.ProjectId);
            if (!allRequirements.Any())
                throw new Exception("No requirements found for this project. Sync Jira issues first (POST /api/jira/projects/{projectId}/sync) or create requirements manually.");

            List<Requirement> selectedRequirements;
            if (dto.RequirementIds != null && dto.RequirementIds.Any())
            {
                selectedRequirements = allRequirements
                    .Where(r => dto.RequirementIds.Contains(r.RequirementId))
                    .ToList();

                if (!selectedRequirements.Any())
                    throw new Exception("None of the specified requirement IDs belong to this project.");
            }
            else
            {
                selectedRequirements = allRequirements.ToList();
            }

            // Auto-generate introduction and scope if not provided
            var introduction = dto.Introduction ??
                $"This Software Requirements Specification (SRS) document describes the functional and non-functional requirements for the \"{project.ProjectName}\" project. " +
                $"It is intended to serve as a reference for the development team and stakeholders.";

            var scope = dto.Scope ??
                $"This document covers all requirements for \"{project.ProjectName}\". " +
                $"It includes {selectedRequirements.Count(r => r.RequirementType == RequirementType.functional)} functional requirement(s) " +
                $"and {selectedRequirements.Count(r => r.RequirementType == RequirementType.non_functional)} non-functional requirement(s).";

            // Create the SRS document header
            var srsDocument = new SrsDocument
            {
                ProjectId = project.ProjectId,
                Version = dto.Version,
                DocumentTitle = dto.DocumentTitle,
                Introduction = introduction,
                Scope = scope,
                ProductPerspective = dto.ProductPerspective,
                UserClasses = dto.UserClasses,
                OperatingEnvironment = dto.OperatingEnvironment,
                AssumptionsDependencies = dto.AssumptionsDependencies,
                Glossary = dto.Glossary,
                UserInterfaces = dto.UserInterfaces,
                HardwareInterfaces = dto.HardwareInterfaces,
                SoftwareInterfaces = dto.SoftwareInterfaces,
                CommunicationsInterfaces = dto.CommunicationsInterfaces,
                PerformanceRequirements = dto.PerformanceRequirements,
                SecurityRequirements = dto.SecurityRequirements,
                SafetyRequirements = dto.SafetyRequirements,
                SoftwareSystemAttributes = dto.SoftwareSystemAttributes,
                Status = DocumentStatus.draft,
                GeneratedBy = userId,
                GeneratedAt = DateTime.UtcNow
            };

            await _srsDocumentRepository.AddAsync(srsDocument);

            // Separate functional and non-functional requirements
            var functional = selectedRequirements
                .Where(r => r.RequirementType == RequirementType.functional)
                .OrderBy(r => r.RequirementCode)
                .ToList();

            var nonFunctional = selectedRequirements
                .Where(r => r.RequirementType == RequirementType.non_functional)
                .OrderBy(r => r.RequirementCode)
                .ToList();

            // Snapshot each requirement into SRS_INCLUDED_REQUIREMENT
            // Section 4.1.X = Functional, 4.2.X = Non-Functional (matches HTML document structure)
            int sectionCounter = 1;
            foreach (var req in functional)
            {
                srsDocument.SrsIncludedRequirements.Add(new SrsIncludedRequirement
                {
                    DocumentId = srsDocument.DocumentId,
                    RequirementId = req.RequirementId,
                    SectionNumber = $"4.1.{sectionCounter}",
                    SnapshotTitle = req.Title,
                    SnapshotDescription = req.Description
                });
                sectionCounter++;
            }

            sectionCounter = 1;
            foreach (var req in nonFunctional)
            {
                srsDocument.SrsIncludedRequirements.Add(new SrsIncludedRequirement
                {
                    DocumentId = srsDocument.DocumentId,
                    RequirementId = req.RequirementId,
                    SectionNumber = $"4.2.{sectionCounter}",
                    SnapshotTitle = req.Title,
                    SnapshotDescription = req.Description
                });
                sectionCounter++;
            }

            await _srsDocumentRepository.UpdateAsync(srsDocument);

            // Reload to get nav properties
            var saved = await _srsDocumentRepository.GetByIdAsync(srsDocument.DocumentId);
            return MapSrsToDTO(saved!);
        }

        /// <summary>
        /// Update SRS document metadata
        /// </summary>
        public async Task<SrsDocumentResponseDTO> UpdateSrsDocumentAsync(int userId, int groupId, int documentId, UpdateSrsDocumentDTO dto)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null) throw new Exception("Group not found");

            await _leaderValidationService.ValidateLeaderAccessAsync(userId, groupId);

            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project == null) throw new Exception("No project found for this group");

            var doc = await _srsDocumentRepository.GetByIdAsync(documentId);
            if (doc == null) throw new Exception("SRS document not found");
            if (doc.ProjectId != project.ProjectId)
                throw new Exception("This SRS document does not belong to your group's project.");

            if (dto.DocumentTitle != null) doc.DocumentTitle = dto.DocumentTitle;
            if (dto.Version != null) doc.Version = dto.Version;
            if (dto.Introduction != null) doc.Introduction = dto.Introduction;
            if (dto.Scope != null) doc.Scope = dto.Scope;
            if (dto.ProductPerspective != null) doc.ProductPerspective = dto.ProductPerspective;
            if (dto.UserClasses != null) doc.UserClasses = dto.UserClasses;
            if (dto.OperatingEnvironment != null) doc.OperatingEnvironment = dto.OperatingEnvironment;
            if (dto.AssumptionsDependencies != null) doc.AssumptionsDependencies = dto.AssumptionsDependencies;
            if (dto.Glossary != null) doc.Glossary = dto.Glossary;
            if (dto.UserInterfaces != null) doc.UserInterfaces = dto.UserInterfaces;
            if (dto.HardwareInterfaces != null) doc.HardwareInterfaces = dto.HardwareInterfaces;
            if (dto.SoftwareInterfaces != null) doc.SoftwareInterfaces = dto.SoftwareInterfaces;
            if (dto.CommunicationsInterfaces != null) doc.CommunicationsInterfaces = dto.CommunicationsInterfaces;
            if (dto.PerformanceRequirements != null) doc.PerformanceRequirements = dto.PerformanceRequirements;
            if (dto.SecurityRequirements != null) doc.SecurityRequirements = dto.SecurityRequirements;
            if (dto.SafetyRequirements != null) doc.SafetyRequirements = dto.SafetyRequirements;
            if (dto.SoftwareSystemAttributes != null) doc.SoftwareSystemAttributes = dto.SoftwareSystemAttributes;
            if (dto.Status != null)
            {
                doc.Status = dto.Status.ToLower() switch
                {
                    "published" => DocumentStatus.published,
                    _ => DocumentStatus.draft
                };
            }

            await _srsDocumentRepository.UpdateAsync(doc);

            var saved = await _srsDocumentRepository.GetByIdAsync(doc.DocumentId);
            return MapSrsToDTO(saved!);
        }

        /// <summary>
        /// Generate a downloadable HTML file of the SRS document
        /// </summary>
        public async Task<(byte[] content, string fileName)> DownloadSrsDocumentAsync(int userId, int groupId, int documentId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null) throw new Exception("Group not found");

            await _leaderValidationService.ValidateLeaderAccessAsync(userId, groupId);

            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project == null) throw new Exception("No project found for this group");

            var doc = await _srsDocumentRepository.GetByIdAsync(documentId);
            if (doc == null) throw new Exception("SRS document not found");
            if (doc.ProjectId != project.ProjectId)
                throw new Exception("This SRS document does not belong to your group's project.");

            var allVersions = await _srsDocumentRepository.GetByProjectIdAsync(project.ProjectId);

            var html = GenerateSrsHtml(doc, project, group, allVersions);
            var bytes = System.Text.Encoding.UTF8.GetBytes(html);
            var safeTitle = doc.DocumentTitle.Replace(" ", "_").Replace("/", "_");
            return (bytes, $"{safeTitle}_v{doc.Version}.html");
        }

        /// <summary>
        /// Generate a downloadable Word-compatible (.doc) file of the SRS document
        /// </summary>
        public async Task<(byte[] content, string fileName)> DownloadSrsDocumentAsDocAsync(int userId, int groupId, int documentId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null) throw new Exception("Group not found");

            await _leaderValidationService.ValidateLeaderAccessAsync(userId, groupId);

            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project == null) throw new Exception("No project found for this group");

            var doc = await _srsDocumentRepository.GetByIdAsync(documentId);
            if (doc == null) throw new Exception("SRS document not found");
            if (doc.ProjectId != project.ProjectId)
                throw new Exception("This SRS document does not belong to your group's project.");

            var allVersions = await _srsDocumentRepository.GetByProjectIdAsync(project.ProjectId);

            var wordHtml = GenerateSrsWordHtml(doc, project, group, allVersions);
            var bytes = System.Text.Encoding.UTF8.GetBytes(wordHtml);
            var safeTitle = doc.DocumentTitle.Replace(" ", "_").Replace("/", "_");
            return (bytes, $"{safeTitle}_v{doc.Version}.doc");
        }

        // ── SRS Helpers ─────────────────────────────────────────────────────────

        /// <summary>
        /// Regenerate the requirement snapshot of an existing SRS document.
        /// Replaces all previously included requirements with the newly selected set,
        /// and updates the Scope text to reflect the new counts.
        /// Does NOT create a new version or change any other metadata.
        /// </summary>
        public async Task<SrsDocumentResponseDTO> RegenerateSrsDocumentAsync(int userId, int groupId, int documentId, RegenerateSrsDocumentDTO dto)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null) throw new Exception("Group not found");

            await _leaderValidationService.ValidateLeaderAccessAsync(userId, groupId);

            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project == null) throw new Exception("No project found for this group");

            var doc = await _srsDocumentRepository.GetByIdAsync(documentId);
            if (doc == null) throw new Exception("SRS document not found");
            if (doc.ProjectId != project.ProjectId)
                throw new Exception("This SRS document does not belong to your group's project.");

            // Auto-import new Jira issues as requirements if requested
            if (dto.ImportFromJira)
            {
                var jiraIssues = await _jiraIssueRepository.GetByProjectIdAsync(project.ProjectId);
                foreach (var issue in jiraIssues)
                {
                    var reqType = TeamLeaderHelper.ClassifyRequirementType(issue.IssueType, issue.Summary, issue.Description);
                    var priority = issue.Priority switch
                    {
                        JiraPriority.highest or JiraPriority.high => PriorityLevel.high,
                        JiraPriority.low or JiraPriority.lowest => PriorityLevel.low,
                        _ => PriorityLevel.medium
                    };

                    var existing = await _requirementRepository.GetByJiraIssueIdAsync(issue.JiraIssueId);
                    if (existing != null)
                    {
                        if (existing.RequirementType != reqType)
                        {
                            existing.RequirementType = reqType;
                            await _requirementRepository.UpdateAsync(existing);
                        }
                        continue;
                    }

                    await _requirementRepository.AddAsync(new Requirement
                    {
                        ProjectId = project.ProjectId,
                        RequirementCode = issue.IssueKey,
                        Title = issue.Summary,
                        Description = issue.Description,
                        JiraIssueId = issue.JiraIssueId,
                        RequirementType = reqType,
                        Priority = priority,
                        CreatedBy = userId
                    });
                }
            }

            // Select requirements to include
            var allRequirements = await _requirementRepository.GetByProjectIdAsync(project.ProjectId);
            if (!allRequirements.Any())
                throw new Exception("No requirements found for this project.");

            List<Requirement> selectedRequirements;
            if (dto.RequirementIds != null && dto.RequirementIds.Any())
            {
                selectedRequirements = allRequirements
                    .Where(r => dto.RequirementIds.Contains(r.RequirementId))
                    .ToList();
                if (!selectedRequirements.Any())
                    throw new Exception("None of the specified requirement IDs belong to this project.");
            }
            else
            {
                selectedRequirements = allRequirements.ToList();
            }

            // Remove old requirement snapshots
            await _srsDocumentRepository.RemoveIncludedRequirementsAsync(documentId);

            // Reload the document (cleared collection) and re-attach
            doc = await _srsDocumentRepository.GetByIdAsync(documentId);
            if (doc == null) throw new Exception("SRS document not found after snapshot removal.");

            var functionalReqs = selectedRequirements
                .Where(r => r.RequirementType == RequirementType.functional)
                .OrderBy(r => r.RequirementCode)
                .ToList();

            var nonFunctionalReqs = selectedRequirements
                .Where(r => r.RequirementType == RequirementType.non_functional)
                .OrderBy(r => r.RequirementCode)
                .ToList();

            // Build new snapshots (section 4.1.X = Functional, 4.2.X = Non-Functional)
            int sectionCounter = 1;
            foreach (var req in functionalReqs)
            {
                doc.SrsIncludedRequirements.Add(new SrsIncludedRequirement
                {
                    DocumentId = doc.DocumentId,
                    RequirementId = req.RequirementId,
                    SectionNumber = $"4.1.{sectionCounter++}",
                    SnapshotTitle = req.Title,
                    SnapshotDescription = req.Description
                });
            }

            sectionCounter = 1;
            foreach (var req in nonFunctionalReqs)
            {
                doc.SrsIncludedRequirements.Add(new SrsIncludedRequirement
                {
                    DocumentId = doc.DocumentId,
                    RequirementId = req.RequirementId,
                    SectionNumber = $"4.2.{sectionCounter++}",
                    SnapshotTitle = req.Title,
                    SnapshotDescription = req.Description
                });
            }

            // Refresh the Scope to reflect updated counts
            doc.Scope = $"This document covers all requirements for \"{project.ProjectName}\". " +
                        $"It includes {functionalReqs.Count} functional requirement(s) " +
                        $"and {nonFunctionalReqs.Count} non-functional requirement(s).";

            await _srsDocumentRepository.UpdateAsync(doc);

            var saved = await _srsDocumentRepository.GetByIdAsync(doc.DocumentId);
            return MapSrsToDTO(saved!);
        }

        private static SrsDocumentResponseDTO MapSrsToDTO(SrsDocument doc) => new()
        {
            DocumentId = doc.DocumentId,
            ProjectId = doc.ProjectId,
            ProjectName = doc.Project?.ProjectName ?? "",
            Version = doc.Version,
            DocumentTitle = doc.DocumentTitle,
            Introduction = doc.Introduction,
            Scope = doc.Scope,
            ProductPerspective = doc.ProductPerspective,
            UserClasses = doc.UserClasses,
            OperatingEnvironment = doc.OperatingEnvironment,
            AssumptionsDependencies = doc.AssumptionsDependencies,
            Glossary = doc.Glossary,
            UserInterfaces = doc.UserInterfaces,
            HardwareInterfaces = doc.HardwareInterfaces,
            SoftwareInterfaces = doc.SoftwareInterfaces,
            CommunicationsInterfaces = doc.CommunicationsInterfaces,
            PerformanceRequirements = doc.PerformanceRequirements,
            SecurityRequirements = doc.SecurityRequirements,
            SafetyRequirements = doc.SafetyRequirements,
            SoftwareSystemAttributes = doc.SoftwareSystemAttributes,
            FilePath = doc.FilePath,
            Status = doc.Status.ToString(),
            GeneratedBy = doc.GeneratedBy,
            GeneratedByName = doc.GeneratedByNavigation?.FullName,
            GeneratedAt = doc.GeneratedAt,
            CreatedAt = doc.CreatedAt,
            UpdatedAt = doc.UpdatedAt,
            Requirements = doc.SrsIncludedRequirements
                .OrderBy(r => r.SectionNumber)
                .Select(r => new SrsIncludedRequirementDTO
                {
                    RequirementId = r.RequirementId,
                    SectionNumber = r.SectionNumber,
                    RequirementCode = r.Requirement?.RequirementCode,
                    Title = r.SnapshotTitle,
                    Description = r.SnapshotDescription,
                    RequirementType = r.Requirement?.RequirementType.ToString(),
                    Priority = r.Requirement?.Priority.ToString()
                }).ToList()
        };

        /// <summary>
        /// Automatically generate SRS document sections using AI based on project requirements.
        /// </summary>
        public async Task<BLL.DTOs.Student.AiSrsResponseDTO> GenerateAiSrsContentAsync(int userId, int groupId, BLL.DTOs.Student.AiSrsRequestDTO dto)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null) throw new Exception("Group not found");

            await _leaderValidationService.ValidateLeaderAccessAsync(userId, groupId);

            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project == null) throw new Exception("No project found for this group");

            // Select requirements to include
            var allRequirements = await _requirementRepository.GetByProjectIdAsync(project.ProjectId);
            if (!allRequirements.Any())
                throw new Exception("No requirements found for this project. Please create or import requirements first.");

            List<Requirement> selectedRequirements;
            if (dto.RequirementIds != null && dto.RequirementIds.Any())
            {
                selectedRequirements = allRequirements
                    .Where(r => dto.RequirementIds.Contains(r.RequirementId))
                    .ToList();
                if (!selectedRequirements.Any())
                    throw new Exception("None of the specified requirement IDs belong to this project.");
            }
            else
            {
                selectedRequirements = allRequirements.ToList();
            }

            var requirementsText = new System.Text.StringBuilder();
            requirementsText.AppendLine($"Project: {project.ProjectName}");
            if (!string.IsNullOrWhiteSpace(project.Description))
            {
                requirementsText.AppendLine($"Project Description: {project.Description}");
            }
            requirementsText.AppendLine("\nRequirements:");
            foreach (var req in selectedRequirements.OrderBy(r => r.RequirementCode))
            {
                requirementsText.AppendLine($"- [{req.RequirementCode}] {req.Title}: {req.Description} (Type: {req.RequirementType}, Priority: {req.Priority})");
            }

            return await _aiChatService.GenerateSrsContentAsync(requirementsText.ToString());
        }

        private static string GenerateSrsHtml(SrsDocument doc, Project project, StudentGroup group, List<SrsDocument> allVersions)
        {
            // Filter by requirement type — more robust than matching SectionNumber prefix
            var functional = doc.SrsIncludedRequirements
                .Where(r => r.Requirement?.RequirementType == RequirementType.functional
                         || (r.Requirement == null && r.SectionNumber != null &&
                             (r.SectionNumber.StartsWith("6.1") || r.SectionNumber.StartsWith("4.1") || r.SectionNumber.StartsWith("3.1"))))
                .OrderBy(r => r.SectionNumber)
                .ToList();

            var nonFunctional = doc.SrsIncludedRequirements
                .Where(r => r.Requirement?.RequirementType == RequirementType.non_functional
                         || (r.Requirement == null && r.SectionNumber != null &&
                             (r.SectionNumber.StartsWith("6.2") || r.SectionNumber.StartsWith("4.2") || r.SectionNumber.StartsWith("3.2"))))
                .OrderBy(r => r.SectionNumber)
                .ToList();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"en\"><head><meta charset=\"UTF-8\">");
            sb.AppendLine($"<title>{Escape(doc.DocumentTitle)}</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("body { font-family: 'Segoe UI', Arial, sans-serif; max-width: 960px; margin: 40px auto; padding: 0 20px; color: #333; line-height: 1.7; }");
            sb.AppendLine("h1 { text-align: center; border-bottom: 3px solid #2c3e50; padding-bottom: 10px; }");
            sb.AppendLine("h2 { color: #2c3e50; border-bottom: 1px solid #bdc3c7; padding-bottom: 5px; margin-top: 35px; }");
            sb.AppendLine("h3 { color: #34495e; margin-top: 20px; }");
            sb.AppendLine("h4 { color: #2c3e50; margin: 0 0 6px 0; }");
            sb.AppendLine(".meta { text-align: center; color: #7f8c8d; margin-bottom: 30px; }");
            sb.AppendLine("table { width: 100%; border-collapse: collapse; margin: 15px 0; }");
            sb.AppendLine("th, td { border: 1px solid #bdc3c7; padding: 10px; text-align: left; vertical-align: top; }");
            sb.AppendLine("th { background-color: #2c3e50; color: white; }");
            sb.AppendLine("tr:nth-child(even) { background-color: #f2f2f2; }");
            sb.AppendLine(".req-section { margin: 10px 0 20px 0; padding: 12px 16px; background: #f9f9f9; border-left: 4px solid #3498db; border-radius: 0 4px 4px 0; }");
            sb.AppendLine(".req-meta { font-size: 0.88em; color: #555; margin: 4px 0 8px 0; }");
            sb.AppendLine(".req-meta span { margin-right: 16px; }");
            sb.AppendLine(".jira-tag { background: #0052cc; color: white; padding: 1px 6px; border-radius: 3px; font-size: 0.82em; font-family: monospace; }");
            sb.AppendLine(".priority-high { color: #e74c3c; font-weight: bold; }");
            sb.AppendLine(".priority-medium { color: #f39c12; font-weight: bold; }");
            sb.AppendLine(".priority-low { color: #27ae60; font-weight: bold; }");
            sb.AppendLine(".toc ol { line-height: 2; }");
            sb.AppendLine(".overall-desc p, .overall-desc ul { margin: 6px 0; }");
            sb.AppendLine(".overall-desc ul { padding-left: 20px; }");
            sb.AppendLine("</style></head><body>");

            // ── Title ──────────────────────────────────────────────────────────
            sb.AppendLine($"<h1>{Escape(doc.DocumentTitle)}</h1>");
            sb.AppendLine("<div class=\"meta\">");
            sb.AppendLine($"<p><strong>Project:</strong> {Escape(project.ProjectName)} &nbsp;|&nbsp; <strong>Group:</strong> {Escape(group.GroupName)} ({Escape(group.GroupCode)})</p>");
            sb.AppendLine($"<p><strong>Version:</strong> {Escape(doc.Version)} &nbsp;|&nbsp; <strong>Status:</strong> {doc.Status}</p>");
            sb.AppendLine($"<p><strong>Generated by:</strong> {Escape(doc.GeneratedByNavigation?.FullName ?? "N/A")} &nbsp;|&nbsp; <strong>Date:</strong> {doc.GeneratedAt?.ToString("yyyy-MM-dd HH:mm") ?? "N/A"} UTC</p>");
            sb.AppendLine("</div>");

            // ── Revision History ───────────────────────────────────────────────
            sb.AppendLine("<h2>Revision History</h2>");
            sb.AppendLine("<table><thead><tr><th>Version</th><th>Date</th><th>Author</th><th>Status</th></tr></thead><tbody>");
            foreach (var v in allVersions.OrderBy(v => v.GeneratedAt))
            {
                var isCurrent = v.DocumentId == doc.DocumentId;
                var rowStyle = isCurrent ? " style=\"font-weight:bold;background:#eaf4fb\"" : "";
                sb.AppendLine($"<tr{rowStyle}>");
                sb.AppendLine($"<td>{Escape(v.Version)}{(isCurrent ? " ◀ current" : "")}</td>");
                sb.AppendLine($"<td>{v.GeneratedAt?.ToString("yyyy-MM-dd") ?? "N/A"}</td>");
                sb.AppendLine($"<td>{Escape(v.GeneratedByNavigation?.FullName ?? "N/A")}</td>");
                sb.AppendLine($"<td>{v.Status}</td>");
                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</tbody></table>");

            // ── Table of Contents ──────────────────────────────────────────────
            sb.AppendLine("<h2>Table of Contents</h2>");
            sb.AppendLine("<div class=\"toc\"><ol>");
            sb.AppendLine("<li>Introduction</li>");
            sb.AppendLine("<li>Glossary and Definitions</li>");
            sb.AppendLine("<li>Scope</li>");
            sb.AppendLine("<li>Overall Description<ol><li>Product Perspective</li><li>User Classes and Characteristics</li><li>Operating Environment</li><li>Assumptions and Dependencies</li></ol></li>");
            sb.AppendLine("<li>External Interface Requirements<ol><li>User Interfaces</li><li>Hardware Interfaces</li><li>Software Interfaces</li><li>Communications Interfaces</li></ol></li>");
            sb.AppendLine("<li>Specific Requirements<ol><li>Functional Requirements</li><li>Non-Functional Requirements</li></ol></li>");
            sb.AppendLine("<li>System Attributes & Other Requirements<ol><li>Performance Requirements</li><li>Security Requirements</li><li>Safety Requirements</li><li>Software System Attributes</li></ol></li>");
            sb.AppendLine("<li>Requirements Summary</li>");
            sb.AppendLine("</ol></div>");

            // ── 1. Introduction ────────────────────────────────────────────────
            sb.AppendLine("<h2>1. Introduction</h2>");
            sb.AppendLine($"<p>{Escape(doc.Introduction ?? "N/A")}</p>");

            // ── 2. Glossary and Definitions ────────────────────────────────────
            sb.AppendLine("<h2>2. Glossary and Definitions</h2>");
            sb.AppendLine($"<p>{Escape(doc.Glossary ?? "N/A")}</p>");

            // ── 3. Scope ───────────────────────────────────────────────────────
            sb.AppendLine("<h2>3. Scope</h2>");
            sb.AppendLine($"<p>{Escape(doc.Scope ?? "N/A")}</p>");

            // ── 4. Overall Description ─────────────────────────────────────────
            sb.AppendLine("<h2>4. Overall Description</h2>");
            sb.AppendLine("<div class=\"overall-desc\">");

            // Detect Jira usage from already-loaded data (no extra DB calls)
            var usesJira = doc.SrsIncludedRequirements.Any(r => r.Requirement?.JiraIssue != null);

            // 4.1 Product Perspective — use stored value or auto-generate
            sb.AppendLine("<h3>4.1 Product Perspective</h3>");
            if (!string.IsNullOrWhiteSpace(doc.ProductPerspective))
            {
                sb.AppendLine($"<p>{Escape(doc.ProductPerspective)}</p>");
            }
            else
            {
                var integrationNote = usesJira
                    ? " Requirements for this project are tracked and synchronized with an external issue tracking system."
                    : "";
                sb.AppendLine($"<p>{Escape(project.ProjectName)} is a software project developed by group " +
                              $"{Escape(group.GroupName)} ({Escape(group.GroupCode)}).{integrationNote} " +
                              $"This document specifies the requirements for the current development cycle.</p>");
            }

            // 4.2 User Classes — use stored value or auto-generate
            sb.AppendLine("<h3>4.2 User Classes and Characteristics</h3>");
            if (!string.IsNullOrWhiteSpace(doc.UserClasses))
            {
                sb.AppendLine($"<p>{Escape(doc.UserClasses)}</p>");
            }
            else
            {
                sb.AppendLine("<p><em>No user classes defined. Provide a description of your system's users " +
                              "via the <code>userClasses</code> field when generating or updating this document.</em></p>");
            }

            // 4.3 Operating Environment — use stored value or generic placeholder
            sb.AppendLine("<h3>4.3 Operating Environment</h3>");
            if (!string.IsNullOrWhiteSpace(doc.OperatingEnvironment))
            {
                sb.AppendLine($"<p>{Escape(doc.OperatingEnvironment)}</p>");
            }
            else
            {
                sb.AppendLine("<p><em>No operating environment specified. Provide details about your tech stack, " +
                              "deployment platform, and browser/device requirements via the " +
                              "<code>operatingEnvironment</code> field.</em></p>");
            }

            // 4.4 Assumptions and Dependencies — use stored value or auto-generate
            sb.AppendLine("<h3>4.4 Assumptions and Dependencies</h3>");
            if (!string.IsNullOrWhiteSpace(doc.AssumptionsDependencies))
            {
                sb.AppendLine($"<p>{Escape(doc.AssumptionsDependencies)}</p>");
            }
            else
            {
                sb.AppendLine("<ul>");
                sb.AppendLine("<li>All users must have valid accounts before accessing any part of the system.</li>");
                sb.AppendLine("<li>Requirements listed in this document are subject to revision pending stakeholder review and approval.</li>");
                sb.AppendLine("<li>The scope of this document is limited to the current project version and may be updated in subsequent releases.</li>");
                if (usesJira)
                    sb.AppendLine("<li><strong>Jira dependency:</strong> This project's requirements are linked to a Jira issue tracking system. " +
                                  "Jira credentials and project access must remain valid for continued requirement synchronization.</li>");
                sb.AppendLine("</ul>");
            }

            sb.AppendLine("</div>");

            // ── 5. External Interface Requirements ─────────────────────────────
            sb.AppendLine("<h2>5. External Interface Requirements</h2>");
            sb.AppendLine("<h3>5.1 User Interfaces</h3>");
            sb.AppendLine($"<p>{Escape(doc.UserInterfaces ?? "N/A")}</p>");
            sb.AppendLine("<h3>5.2 Hardware Interfaces</h3>");
            sb.AppendLine($"<p>{Escape(doc.HardwareInterfaces ?? "N/A")}</p>");
            sb.AppendLine("<h3>5.3 Software Interfaces</h3>");
            sb.AppendLine($"<p>{Escape(doc.SoftwareInterfaces ?? "N/A")}</p>");
            sb.AppendLine("<h3>5.4 Communications Interfaces</h3>");
            sb.AppendLine($"<p>{Escape(doc.CommunicationsInterfaces ?? "N/A")}</p>");

            // ── 6. Specific Requirements ───────────────────────────────────────
            sb.AppendLine("<h2>6. Specific Requirements</h2>");

            sb.AppendLine("<h3>6.1 Functional Requirements</h3>");
            AppendRequirementBlocks(sb, functional);

            sb.AppendLine("<h3>6.2 Non-Functional Requirements</h3>");
            AppendRequirementBlocks(sb, nonFunctional);

            // ── 7. System Attributes & Other Requirements ──────────────────────
            sb.AppendLine("<h2>7. System Attributes & Other Requirements</h2>");
            sb.AppendLine("<h3>7.1 Performance Requirements</h3>");
            sb.AppendLine($"<p>{Escape(doc.PerformanceRequirements ?? "N/A")}</p>");
            sb.AppendLine("<h3>7.2 Security Requirements</h3>");
            sb.AppendLine($"<p>{Escape(doc.SecurityRequirements ?? "N/A")}</p>");
            sb.AppendLine("<h3>7.3 Safety Requirements</h3>");
            sb.AppendLine($"<p>{Escape(doc.SafetyRequirements ?? "N/A")}</p>");
            sb.AppendLine("<h3>7.4 Software System Attributes</h3>");
            sb.AppendLine($"<p>{Escape(doc.SoftwareSystemAttributes ?? "N/A")}</p>");

            // ── 8. Requirements Summary ────────────────────────────────────────
            sb.AppendLine("<h2>8. Requirements Summary</h2>");
            sb.AppendLine("<table><thead><tr><th>ID</th><th>Code</th><th>Title</th><th>Type</th><th>Priority</th><th>Jira</th></tr></thead><tbody>");
            foreach (var req in doc.SrsIncludedRequirements.OrderBy(r => r.SectionNumber))
            {
                var jiraKey = req.Requirement?.JiraIssue?.IssueKey ?? "-";
                sb.AppendLine("<tr>");
                sb.AppendLine($"<td>{Escape(req.SectionNumber ?? "")}</td>");
                sb.AppendLine($"<td>{Escape(req.Requirement?.RequirementCode ?? "")}</td>");
                sb.AppendLine($"<td>{Escape(req.SnapshotTitle ?? "")}</td>");
                sb.AppendLine($"<td>{req.Requirement?.RequirementType}</td>");
                sb.AppendLine($"<td>{req.Requirement?.Priority}</td>");
                sb.AppendLine($"<td>{Escape(jiraKey)}</td>");
                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</tbody></table>");

            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        /// <summary>
        /// Generates Word-compatible HTML (opens natively in Microsoft Word)
        /// </summary>
        private static string GenerateSrsWordHtml(SrsDocument doc, Project project, StudentGroup group, List<SrsDocument> allVersions)
        {
            // Generate the body content from the standard HTML generator then wrap with Word namespaces
            var innerHtml = GenerateSrsHtml(doc, project, group, allVersions);

            // Strip the existing html/head tags and inject Word-specific ones
            var bodyStart = innerHtml.IndexOf("<body>", StringComparison.OrdinalIgnoreCase);
            var bodyContent = bodyStart >= 0 ? innerHtml.Substring(bodyStart) : innerHtml;

            var wordStyles = @"
@page { margin: 2.5cm; }
body { font-family: 'Times New Roman', serif; font-size: 12pt; }
h1 { font-size: 18pt; }
h2 { font-size: 14pt; }
h3 { font-size: 13pt; }
h4 { font-size: 12pt; }
table { border-collapse: collapse; width: 100%; }
th, td { border: 1px solid #000; padding: 6pt; }
th { background-color: #2c3e50; color: white; }
.req-section { border-left: 4px solid #2c3e50; padding: 8pt; margin: 8pt 0; background: #f5f5f5; }
.priority-high { color: #c0392b; font-weight: bold; }
.priority-medium { color: #d68910; font-weight: bold; }
.priority-low { color: #1e8449; font-weight: bold; }
.jira-tag { background: #0052cc; color: white; padding: 1pt 4pt; font-size: 10pt; }";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<html xmlns:o=\"urn:schemas-microsoft-com:office:office\"");
            sb.AppendLine("      xmlns:w=\"urn:schemas-microsoft-com:office:word\"");
            sb.AppendLine("      xmlns=\"http://www.w3.org/TR/REC-html40\">");
            sb.AppendLine("<head><meta charset=\"UTF-8\">");
            sb.AppendLine("<meta name=\"ProgId\" content=\"Word.Document\">");
            sb.AppendLine("<meta name=\"Generator\" content=\"Microsoft Word\">");
            sb.AppendLine("<!--[if gte mso 9]><xml><w:WordDocument>");
            sb.AppendLine("  <w:View>Print</w:View><w:Zoom>100</w:Zoom>");
            sb.AppendLine("  <w:DoNotOptimizeForBrowser/>");
            sb.AppendLine("</w:WordDocument></xml><![endif]-->");
            sb.AppendLine($"<title>{Escape(doc.DocumentTitle)}</title>");
            sb.AppendLine($"<style>{wordStyles}</style>");
            sb.AppendLine("</head>");
            sb.AppendLine(bodyContent);
            sb.AppendLine("</html>");
            return sb.ToString();
        }

        private static void AppendRequirementBlocks(System.Text.StringBuilder sb, List<SrsIncludedRequirement> reqs)
        {
            if (!reqs.Any())
            {
                sb.AppendLine("<p><em>No requirements in this category.</em></p>");
                return;
            }
            foreach (var req in reqs)
            {
                var priorityClass = GetPriorityClass(req.Requirement?.Priority);
                var jiraKey = req.Requirement?.JiraIssue?.IssueKey;
                sb.AppendLine("<div class=\"req-section\">");
                sb.AppendLine($"<h4>{Escape(req.SectionNumber ?? "")} — {Escape(req.SnapshotTitle ?? "Untitled")}</h4>");
                sb.AppendLine("<div class=\"req-meta\">");
                sb.AppendLine($"<span><strong>Code:</strong> {Escape(req.Requirement?.RequirementCode ?? "N/A")}</span>");
                sb.AppendLine($"<span><strong>Priority:</strong> <span class=\"{priorityClass}\">{req.Requirement?.Priority}</span></span>");
                if (!string.IsNullOrEmpty(jiraKey))
                    sb.AppendLine($"<span><strong>Jira:</strong> <span class=\"jira-tag\">{Escape(jiraKey)}</span></span>");
                sb.AppendLine("</div>");
                sb.AppendLine($"<p>{Escape(req.SnapshotDescription ?? "No description provided.")}</p>");
                sb.AppendLine("</div>");
            }
        }

        private static string Escape(string? text) =>
            System.Net.WebUtility.HtmlEncode(text ?? "").Replace("\r\n", "<br />").Replace("\n", "<br />");

        private static string GetPriorityClass(PriorityLevel? priority) =>
            priority switch
            {
                PriorityLevel.high => "priority-high",
                PriorityLevel.medium => "priority-medium",
                PriorityLevel.low => "priority-low",
                _ => ""
            };

    }
}
