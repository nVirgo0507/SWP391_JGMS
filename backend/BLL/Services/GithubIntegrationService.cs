using BLL.Services.Interface;
using DAL.Models;
using DAL.Repositories.Interface;
using System;
using System.Linq;
using System.Text.Json;
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
                DateTimeOffset? since = null;
                
                if (integration?.LastSync != null)
                {
                    // Add a small buffer of 1 minute to ensure no commits are missed during edge cases
                    since = new DateTimeOffset(integration.LastSync.Value.AddMinutes(-1));
                }

                var commits = await _githubApiService.GetCommitsAsync(projectId, since);

                if (commits == null || !commits.Any())
                {
                    await UpdateLastSyncAsync(projectId, SyncStatus.success);
                    return;
                }

                //Fetch existing commit SHAs for this project to avoid N+1 queries
                var existingGithubCommits = await _githubCommitRepository.GetCommitsByProjectIdAsync(projectId);
                var existingShas = existingGithubCommits.Select(c => c.CommitSha).ToHashSet();

                //Fetch all users for author mapping in memory
                var allUsers = await _userRepository.GetAllAsync();
                var usersByGithubUsername = allUsers
                    .Where(u => !string.IsNullOrEmpty(u.GithubUsername))
                    .ToDictionary(u => u.GithubUsername, u => u, StringComparer.OrdinalIgnoreCase);
                var usersByEmail = allUsers
                    .Where(u => !string.IsNullOrEmpty(u.Email))
                    .ToDictionary(u => u.Email, u => u, StringComparer.OrdinalIgnoreCase);

                var newGithubCommits = new System.Collections.Generic.List<GithubCommit>();
                
                // Group by SHA to get distinct commits (in case the API returns duplicates)
                var uniqueCommits = commits.GroupBy(c => c.Sha).Select(g => g.First()).ToList();
                var dtosBySha = uniqueCommits.ToDictionary(c => c.Sha, c => c);

                foreach (var dto in uniqueCommits)
                {
                    if (existingShas.Contains(dto.Sha))
                        continue;

                    // 1. Save to GithubCommit (raw data)
                    var githubCommit = new GithubCommit
                    {
                        ProjectId = projectId,
                        CommitSha = dto.Sha,
                        CommitMessage = dto.Message,
                        AuthorUsername = dto.AuthorName,
                        AuthorEmail = dto.AuthorEmail,
                        CommitDate = dto.Date,
                        Additions = dto.Additions,
                        Deletions = dto.Deletions,
                        ChangedFiles = dto.ChangedFiles,
                        LastSynced = DateTime.UtcNow
                    };

                    newGithubCommits.Add(githubCommit);
                }

                if (newGithubCommits.Any())
                {
                    await _githubCommitRepository.AddRangeAsync(newGithubCommits);

                    // 2. Try to map to internal User and create base Commit
                    var newBaseCommits = new System.Collections.Generic.List<Commit>();

                    foreach (var githubCommit in newGithubCommits)
                    {
                        var dto = dtosBySha[githubCommit.CommitSha];

                        User? user = null;
                        if (!string.IsNullOrEmpty(dto.AuthorName) && usersByGithubUsername.TryGetValue(dto.AuthorName, out var userByGithub))
                        {
                            user = userByGithub;
                        }
                        else if (!string.IsNullOrEmpty(dto.AuthorEmail) && usersByEmail.TryGetValue(dto.AuthorEmail, out var userByEmail))
                        {
                            user = userByEmail;
                        }

                        if (user != null)
                        {
                            var baseCommit = new Commit
                            {
                                ProjectId = projectId,
                                UserId = user.UserId,
                                GithubCommitId = githubCommit.GithubCommitId,
                                CommitMessage = dto.Message,
                                CommitDate = dto.Date,
                                Additions = dto.Additions,
                                Deletions = dto.Deletions,
                                ChangedFiles = dto.ChangedFiles
                            };

                            newBaseCommits.Add(baseCommit);
                        }
                    }

                    if (newBaseCommits.Any())
                    {
                        await _commitRepository.AddRangeAsync(newBaseCommits);
                    }
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

        private async Task<User?> MapGithubAuthorToUserAsync(string githubUsername, string email)
        {
            // First try by exact GithubUsername
            if (!string.IsNullOrEmpty(githubUsername))
            {
                var userByGithub = await _userRepository.GetByGithubUsernameAsync(githubUsername);
                if (userByGithub != null) return userByGithub;
            }

            // Then try by Email
            if (!string.IsNullOrEmpty(email))
            {
                var userByEmail = await _userRepository.GetByEmailAsync(email);
                if (userByEmail != null) return userByEmail;
            }

            return null;
        }
    }
}
