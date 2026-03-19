using BLL.Services.Interface;
using DAL.Models;
using DAL.Repositories.Interface;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BLL.Services
{
    public class GithubIntegrationService : IGithubIntegrationService
    {
        private readonly IGithubApiService _githubApiService;
        private readonly IGithubCommitRepository _githubCommitRepository;
        private readonly ICommitRepository _commitRepository;
        private readonly IUserRepository _userRepository;
        private readonly IGithubIntegrationRepository _githubIntegrationRepository;

        public GithubIntegrationService(
            IGithubApiService githubApiService,
            IGithubCommitRepository githubCommitRepository,
            ICommitRepository commitRepository,
            IUserRepository userRepository,
            IGithubIntegrationRepository githubIntegrationRepository)
        {
            _githubApiService = githubApiService;
            _githubCommitRepository = githubCommitRepository;
            _commitRepository = commitRepository;
            _userRepository = userRepository;
            _githubIntegrationRepository = githubIntegrationRepository;
        }

        public async System.Threading.Tasks.Task SyncCommitsAsync(int projectId)
        {
            // Mark as syncing
            await SetSyncStatusAsync(projectId, SyncStatus.syncing);

            try
            {
                var integration = await _githubIntegrationRepository.GetByProjectIdAsync(projectId);
                var since = integration?.LastSync;
                var commits = await _githubApiService.GetCommitsAsync(projectId, since);

                if (!commits.Any())
                {
                    await UpdateLastSyncAsync(projectId, SyncStatus.success);
                    return;
                }

                var newGithubCommits = new List<(BLL.DTOs.Github.GithubCommitDto dto, GithubCommit entity)>();

                foreach (var dto in commits)
                {
                    if (await _githubCommitRepository.CommitExistsAsync(dto.Sha))
                        continue;

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
                }

                var newBaseCommits = new List<Commit>();

                foreach (var item in newGithubCommits)
                {
                    var dto = item.dto;
                    var githubCommit = item.entity;

                    // 2. Try to map to internal User and create base Commit
                    var user = await MapGithubAuthorToUserAsync(dto.AuthorLogin, dto.AuthorEmail, dto.AuthorName);
                    if (user != null)
                    {
                        newBaseCommits.Add(new Commit
                        {
                            ProjectId = projectId,
                            UserId = user.UserId,
                            GithubCommitId = githubCommit.GithubCommitId,
                            CommitMessage = dto.Message,
                            CommitDate = dto.Date,
                            Additions = dto.Additions,
                            Deletions = dto.Deletions,
                            ChangedFiles = dto.ChangedFiles
                        });
                    }
                }

                if (newBaseCommits.Any())
                {
                    await _commitRepository.AddRangeAsync(newBaseCommits);
                }

                // Mark sync as successful
                await UpdateLastSyncAsync(projectId, SyncStatus.success);
            }
            catch
            {
                // Mark sync as failed, then re-throw
                await SetSyncStatusAsync(projectId, SyncStatus.failed);
                throw;
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
                // In a real scenario, we would parse the payload using System.Text.Json
                // extract the repository matching our GithubIntegration
                // and extract the commits array to insert them similarly to SyncCommitsAsync.
                // For simplicity in this implementation, we will log or handle basic parsing.
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
