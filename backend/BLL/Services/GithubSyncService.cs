using BLL.DTOs.Admin;
using BLL.Services.Interface;
using DAL.Models;
using DAL.Repositories.Interface;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BLL.Services
{
    /// <summary>
    /// Synchronizes raw GitHub commits into the normalized COMMIT table.
    /// BR-040: GitHub Username Matching Required - only link commits when author_username matches USER.github_username.
    /// </summary>
    public class GithubSyncService : IGithubSyncService
    {
        private readonly IGithubCommitRepository _githubCommitRepo;
        private readonly IUserRepository _userRepository;
        private readonly ICommitRepository _commitRepository;

        public GithubSyncService(
            IGithubCommitRepository githubCommitRepo,
            IUserRepository userRepository,
            ICommitRepository commitRepository)
        {
            _githubCommitRepo = githubCommitRepo;
            _userRepository = userRepository;
            _commitRepository = commitRepository;
        }

        public async Task<CommitSyncResultDTO> SyncCommitsAsync(int projectId)
        {
            var rawCommits = await _githubCommitRepo.GetCommitsByProjectIdAsync(projectId);
            var result = new CommitSyncResultDTO
            {
                ProjectId = projectId,
                RawCommitsScanned = rawCommits.Count,
                SyncedAt = DateTime.UtcNow
            };

            // Cache users by github username for fast lookup (case-insensitive)
            var githubUsers = (await _userRepository.GetAllAsync())
                .Where(u => !string.IsNullOrWhiteSpace(u.GithubUsername))
                .GroupBy(u => u.GithubUsername!.ToLowerInvariant())
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var raw in rawCommits)
            {
                // Skip if already linked
                if (await _commitRepository.ExistsByGithubCommitIdAsync(raw.GithubCommitId))
                {
                    result.CommitsAlreadyLinked++;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(raw.AuthorUsername))
                {
                    result.CommitsSkippedNoUserMatch++;
                    continue;
                }

                // Match GitHub username to system user (case-insensitive)
                var key = raw.AuthorUsername.Trim().ToLowerInvariant();
                if (!githubUsers.TryGetValue(key, out var user))
                {
                    result.CommitsSkippedNoUserMatch++;
                    continue;
                }

                // Create linked commit record
                var commit = new Commit
                {
                    UserId = user.UserId,
                    GithubCommitId = raw.GithubCommitId,
                    ProjectId = raw.ProjectId,
                    CommitMessage = raw.CommitMessage,
                    Additions = raw.Additions,
                    Deletions = raw.Deletions,
                    ChangedFiles = raw.ChangedFiles,
                    CommitDate = raw.CommitDate,
                    CreatedAt = DateTime.UtcNow
                };

                await _commitRepository.AddAsync(commit);
                result.CommitsLinked++;
            }

            return result;
        }

        public async Task<CommitSyncResultDTO> ImportRawCommitsAsync(int projectId, List<DTOs.Admin.GithubRawCommitDTO> rawCommits)
        {
            var result = new CommitSyncResultDTO
            {
                ProjectId = projectId,
                RawCommitsScanned = rawCommits.Count,
                SyncedAt = DateTime.UtcNow
            };

            foreach (var raw in rawCommits)
            {
                if (string.IsNullOrWhiteSpace(raw.CommitSha))
                {
                    continue;
                }

                // enforce BR-041: unique commit SHA
                if (await _githubCommitRepo.CommitExistsAsync(raw.CommitSha))
                {
                    result.CommitsAlreadyLinked++;
                    continue;
                }

                var entity = new GithubCommit
                {
                    ProjectId = projectId,
                    CommitSha = raw.CommitSha,
                    AuthorUsername = raw.AuthorUsername ?? string.Empty,
                    AuthorEmail = raw.AuthorEmail,
                    CommitMessage = raw.CommitMessage,
                    Additions = raw.Additions,
                    Deletions = raw.Deletions,
                    ChangedFiles = raw.ChangedFiles,
                    CommitDate = raw.CommitDate,
                    BranchName = raw.BranchName,
                    CreatedAt = DateTime.UtcNow
                };

                await _githubCommitRepo.AddAsync(entity);
                result.CommitsLinked++;
            }

            return result;
        }
    }
}
