using BLL.Services.Interface;
using DAL.Models;
using DAL.Repositories.Interface;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BLL.DTOs.Github;

namespace BLL.Services
{
    public class GithubIntegrationService : IGithubIntegrationService
    {
        private readonly IGithubApiService _githubApiService;
        private readonly IGithubCommitRepository _githubCommitRepository;
        private readonly ICommitRepository _commitRepository;
        private readonly IUserRepository _userRepository;
        private readonly IGithubIntegrationRepository _githubIntegrationRepository;
        private readonly ICommitStatisticRepository _commitStatisticRepository;
        private readonly IProjectRepository _projectRepository;

        public GithubIntegrationService(
            IGithubApiService githubApiService,
            IGithubCommitRepository githubCommitRepository,
            ICommitRepository commitRepository,
            IUserRepository userRepository,
            IGithubIntegrationRepository githubIntegrationRepository,
            ICommitStatisticRepository commitStatisticRepository,
            IProjectRepository projectRepository)
        {
            _githubApiService = githubApiService;
            _githubCommitRepository = githubCommitRepository;
            _commitRepository = commitRepository;
            _userRepository = userRepository;
            _githubIntegrationRepository = githubIntegrationRepository;
            _commitStatisticRepository = commitStatisticRepository;
            _projectRepository = projectRepository;
        }

        public async Task<GithubSyncSummaryDto> SyncCommitsAsync(int projectId)
        {
            var startedAt = DateTime.UtcNow;
            var integration = await _githubIntegrationRepository.GetByProjectIdAsync(projectId);
            if (integration == null)
            {
                throw new Exception($"GitHub integration not found for project {projectId}");
            }

            // BR-025: Sync Interval Limits (at least 5 minutes)
            if (integration.LastSync.HasValue && (DateTime.UtcNow - integration.LastSync.Value).TotalMinutes < 5)
            {
                throw new Exception("Please wait at least 5 minutes between manual syncs");
            }

            // Mark as syncing
            await SetSyncStatusAsync(projectId, SyncStatus.syncing);

            try
            {
                var integration = await _githubIntegrationRepository.GetByProjectIdAsync(projectId);
                var since = integration?.LastSync;
                var commits = await _githubApiService.GetCommitsAsync(projectId, since);
                var fetchedCount = commits.Count;

                // Existing raw commits in DB (used for SHA dedupe and local-commit recovery).
                var existingGithubCommits = await _githubCommitRepository.GetCommitsByProjectIdAsync(projectId);
                var existingShas = existingGithubCommits
                    .Select(c => c.CommitSha)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var newGithubCommits = new List<(BLL.DTOs.Github.GithubCommitDto dto, GithubCommit entity)>();
                var duplicateSkipped = 0;

                // Merge note: keep the dedupe set and tuple list declared above.
                
                // Group by SHA to get distinct commits (in case the API returns duplicates)
                var uniqueCommits = commits.GroupBy(c => c.Sha).Select(g => g.First()).ToList();
                var dtosBySha = uniqueCommits.ToDictionary(c => c.Sha, c => c);

                foreach (var dto in uniqueCommits)
                {
                    // BR-041: Unique Commit SHA
                    if (existingShas.Contains(dto.Sha) || await _githubCommitRepository.CommitExistsAsync(dto.Sha))
                    {
                        duplicateSkipped++;
                        continue;
                    }

                    existingShas.Add(dto.Sha);

                    // BR-043: Commit Date Validation
                    if (dto.Date > DateTime.UtcNow)
                    {
                        throw new Exception("Invalid commit date detected");
                    }

                    // BR-044: Non-Negative Code Changes
                    if (dto.Additions < 0 || dto.Deletions < 0 || dto.ChangedFiles < 0)
                    {
                        continue;
                    }
                    var githubCommit = new GithubCommit
                    {
                        ProjectId = projectId,
                        CommitSha = dto.Sha,
                        CommitMessage = dto.Message,
                        AuthorUsername = NormalizeUsername(dto.AuthorLogin)
                                         ?? NormalizeUsername(dto.AuthorName)
                                         ?? "unknown",
                        AuthorEmail = NormalizeEmail(dto.AuthorEmail),
                        CommitDate = dto.Date,
                        Additions = dto.Additions,
                        Deletions = dto.Deletions,
                        ChangedFiles = dto.ChangedFiles,
                        LastSynced = DateTime.UtcNow
                    };

                    newGithubCommits.Add((dto, githubCommit));
                }

                if (newGithubCommits.Any())
                {
                    await _githubCommitRepository.AddRangeAsync(newGithubCommits.Select(x => x.entity));
                    existingGithubCommits.AddRange(newGithubCommits.Select(x => x.entity));
                }

                var newBaseCommits = new List<Commit>();

                // Rebuild missing local commits from any raw github_commit rows that are not linked yet.
                var existingLocalCommitGithubIds = (await _commitRepository.GetCommitsByProjectIdAsync(projectId))
                    .Select(c => c.GithubCommitId)
                    .ToHashSet();

                var pendingRawForLocal = existingGithubCommits
                    .Where(gc => !existingLocalCommitGithubIds.Contains(gc.GithubCommitId))
                    .ToList();
                var unmatchedLocalCommits = 0;

                foreach (var githubCommit in pendingRawForLocal)
                {
                    var user = await MapGithubAuthorToUserAsync(
                        githubCommit.AuthorUsername,
                        githubCommit.AuthorEmail,
                        githubCommit.AuthorUsername);

                    if (user != null)
                    {
                        newBaseCommits.Add(new Commit
                        {
                            ProjectId = projectId,
                            UserId = user.UserId,
                            GithubCommitId = githubCommit.GithubCommitId,
                            CommitMessage = githubCommit.CommitMessage,
                            CommitDate = githubCommit.CommitDate,
                            Additions = githubCommit.Additions,
                            Deletions = githubCommit.Deletions,
                            ChangedFiles = githubCommit.ChangedFiles,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        unmatchedLocalCommits++;
                    }
                }

                if (newBaseCommits.Any())
                {
                    await _commitRepository.AddRangeAsync(newBaseCommits);
                }

                // Mark sync as successful
                await UpdateLastSyncAsync(projectId, SyncStatus.success);

                // Auto-sync statistics after commits are updated
                await SyncCommitStatisticsAsync(projectId);

                return new GithubSyncSummaryDto
                {
                    ProjectId = projectId,
                    IncrementalSync = since.HasValue,
                    Since = since,
                    GithubFetched = fetchedCount,
                    DuplicateShaSkipped = duplicateSkipped,
                    NewRawGithubCommits = newGithubCommits.Count,
                    LocalCommitsRecovered = newBaseCommits.Count,
                    UnmatchedLocalCommits = unmatchedLocalCommits,
                    ElapsedMilliseconds = (long)(DateTime.UtcNow - startedAt).TotalMilliseconds
                };
            }
            catch
            {
                // Mark sync as failed, then re-throw
                await SetSyncStatusAsync(projectId, SyncStatus.failed);
                throw;
            }
        }

        public async System.Threading.Tasks.Task SyncCommitStatisticsAsync(int projectId)
        {
            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project == null) return;

            // Period is Project Start to Project End (or Today if End is future/null)
            var pStart = project.StartDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
            var pEnd = project.EndDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            if (pEnd < pStart) pEnd = pStart.AddDays(30);

            var totalDays = (pEnd.ToDateTime(TimeOnly.MinValue) - pStart.ToDateTime(TimeOnly.MinValue)).TotalDays;
            if (totalDays <= 0) totalDays = 1;

            // Get all linked commits for this project
            var commits = await _commitRepository.GetCommitsByProjectIdAsync(projectId);

            // Group by user
            var userGroups = commits.GroupBy(c => c.UserId);

            foreach (var group in userGroups)
            {
                var userId = group.Key;
                var totalCommits = group.Count();
                var totalAdditions = group.Sum(c => c.Additions ?? 0);
                var totalDeletions = group.Sum(c => c.Deletions ?? 0);
                var totalChangedFiles = group.Sum(c => c.ChangedFiles ?? 0);

                // BR-051: Commit Frequency Calculation = total_commits / days
                // Calculate as decimal with 2 decimal places
                var frequency = Math.Round((decimal)totalCommits / (decimal)totalDays, 2);

                var avgSize = 0;
                if (totalCommits > 0)
                {
                    avgSize = (totalAdditions + totalDeletions) / totalCommits;
                }

                var existingStat = await _commitStatisticRepository.GetByUserProjectAndPeriodAsync(userId, projectId, pStart, pEnd);

                if (existingStat != null)
                {
                    existingStat.TotalCommits = totalCommits;
                    existingStat.TotalAdditions = totalAdditions;
                    existingStat.TotalDeletions = totalDeletions;
                    existingStat.TotalChangedFiles = totalChangedFiles;
                    existingStat.CommitFrequency = frequency;
                    existingStat.AvgCommitSize = avgSize;
                    existingStat.UpdatedAt = DateTime.UtcNow;
                    await _commitStatisticRepository.UpdateAsync(existingStat);
                }
                else
                {
                    var stat = new CommitStatistic
                    {
                        ProjectId = projectId,
                        UserId = userId,
                        PeriodStart = pStart,
                        PeriodEnd = pEnd,
                        TotalCommits = totalCommits,
                        TotalAdditions = totalAdditions,
                        TotalDeletions = totalDeletions,
                        TotalChangedFiles = totalChangedFiles,
                        CommitFrequency = frequency,
                        AvgCommitSize = avgSize,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    await _commitStatisticRepository.AddAsync(stat);
                }
            }
        }

        private async System.Threading.Tasks.Task SetSyncStatusAsync(int projectId, SyncStatus status)
        {
            var integration = await _githubIntegrationRepository.GetByProjectIdAsync(projectId);
            if (integration != null)
            {
                integration.SyncStatus = status;
                integration.UpdatedAt = DateTime.UtcNow;
                await _githubIntegrationRepository.UpdateAsync(integration);
            }
        }

        private async System.Threading.Tasks.Task UpdateLastSyncAsync(int projectId, SyncStatus status)
        {
            var integration = await _githubIntegrationRepository.GetByProjectIdAsync(projectId);
            if (integration != null)
            {
                integration.LastSync = DateTime.UtcNow;
                integration.SyncStatus = status;
                integration.UpdatedAt = DateTime.UtcNow;
                await _githubIntegrationRepository.UpdateAsync(integration);
            }
        }

        public async System.Threading.Tasks.Task ProcessWebhookEventAsync(string eventType, string payload)
        {
            if (eventType == "push")
            {
                // Webhook logic...
                await System.Threading.Tasks.Task.CompletedTask;
            }
        }

        private async Task<User?> MapGithubAuthorToUserAsync(string? githubLogin, string? email, string? authorName)
        {
            // 1) Primary: GitHub login -> local github_username
            var normalizedLogin = NormalizeUsername(githubLogin);
            if (!string.IsNullOrEmpty(normalizedLogin))
            {
                var userByGithub = await _userRepository.GetByGithubUsernameAsync(normalizedLogin);
                if (userByGithub != null) return userByGithub;
            }

            // 2) Fallback: normalized commit email -> local email
            var normalizedEmail = NormalizeEmail(email);
            if (!string.IsNullOrEmpty(normalizedEmail))
            {
                var userByEmail = await _userRepository.GetByEmailAsync(normalizedEmail);
                if (userByEmail != null) return userByEmail;

                // 3) Optional fallback for GitHub noreply addresses.
                var noreplyLogin = ExtractLoginFromNoReplyEmail(normalizedEmail);
                if (!string.IsNullOrEmpty(noreplyLogin))
                {
                    var userByNoReplyLogin = await _userRepository.GetByGithubUsernameAsync(noreplyLogin);
                    if (userByNoReplyLogin != null) return userByNoReplyLogin;
                }
            }

            // 4) Last resort: use author display string only if it looks like a login.
            var authorNameAsLogin = NormalizeUsername(authorName);
            if (!string.IsNullOrEmpty(authorNameAsLogin) && LooksLikeGithubLogin(authorNameAsLogin))
            {
                var userByHeuristicLogin = await _userRepository.GetByGithubUsernameAsync(authorNameAsLogin);
                if (userByHeuristicLogin != null) return userByHeuristicLogin;
            }

            return null;
        }

        private static string? NormalizeEmail(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            return value.Trim().ToLowerInvariant();
        }

        private static string? NormalizeUsername(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            return value.Trim().ToLowerInvariant();
        }

        private static bool LooksLikeGithubLogin(string value)
        {
            // GitHub login allows alnum/hyphen, no spaces. Keep this strict to avoid false matches on real names.
            return Regex.IsMatch(value, "^[a-z0-9](?:[a-z0-9-]{0,37})$");
        }

        private static string? ExtractLoginFromNoReplyEmail(string email)
        {
            // Handles both:
            // 12345+login@users.noreply.github.com
            // login@users.noreply.github.com
            const string suffix = "@users.noreply.github.com";
            if (!email.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return null;

            var localPart = email[..^suffix.Length];
            if (string.IsNullOrWhiteSpace(localPart))
                return null;

            var plusIndex = localPart.IndexOf('+');
            var login = plusIndex >= 0 ? localPart[(plusIndex + 1)..] : localPart;
            login = login.Trim().ToLowerInvariant();
            return LooksLikeGithubLogin(login) ? login : null;
        }
    }
}
