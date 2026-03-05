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

        public GithubIntegrationService(
            IGithubApiService githubApiService,
            IGithubCommitRepository githubCommitRepository,
            ICommitRepository commitRepository,
            IUserRepository userRepository)
        {
            _githubApiService = githubApiService;
            _githubCommitRepository = githubCommitRepository;
            _commitRepository = commitRepository;
            _userRepository = userRepository;
        }

        public async System.Threading.Tasks.Task SyncCommitsAsync(int projectId)
        {
            var commits = await _githubApiService.GetCommitsAsync(projectId);

            if (commits == null || !commits.Any())
                return;

            foreach (var dto in commits)
            {
                if (await _githubCommitRepository.CommitExistsAsync(dto.Sha))
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
                    ChangedFiles = dto.ChangedFiles
                };

                await _githubCommitRepository.AddAsync(githubCommit);

                // 2. Try to map to internal User and create base Commit
                var user = await MapGithubAuthorToUserAsync(dto.AuthorName, dto.AuthorEmail);
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

                    await _commitRepository.AddAsync(baseCommit);
                }
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
