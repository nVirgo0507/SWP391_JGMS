using BLL.DTOs.Github;
using BLL.Services.Interface;
using DAL.Repositories.Interface;
using Octokit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BLL.Services
{
    public class GithubApiService : IGithubApiService
    {
        private readonly IGithubIntegrationRepository _integrationRepository;
        private readonly ITokenEncryptionService _tokenEncryption;

        public GithubApiService(IGithubIntegrationRepository integrationRepository, ITokenEncryptionService tokenEncryptionService)
        {
            _integrationRepository = integrationRepository;
            _tokenEncryption = tokenEncryptionService;
        }

        private async Task<GitHubClient> GetClientAsync(int projectId)
        {
            var integration = await _integrationRepository.GetByProjectIdAsync(projectId);
            if (integration == null || string.IsNullOrEmpty(integration.ApiToken))
            {
                throw new Exception($"GitHub integration not found or token missing for project {projectId}");
            }

            var decryptedToken = _tokenEncryption.Decrypt(integration.ApiToken);

            var client = new GitHubClient(new ProductHeaderValue("JGMS"));
            client.Credentials = new Credentials(decryptedToken);

            return client;
        }

        private async Task<(string Owner, string Repo)> GetRepoInfoAsync(int projectId)
        {
            var integration = await _integrationRepository.GetByProjectIdAsync(projectId);
            if (integration == null || string.IsNullOrEmpty(integration.RepoOwner) || string.IsNullOrEmpty(integration.RepoName))
            {
                throw new Exception($"GitHub repository info missing for project {projectId}");
            }
            return (integration.RepoOwner, integration.RepoName);
        }

        public async Task<List<GithubBranchDto>> GetBranchesAsync(int projectId)
        {
            var client = await GetClientAsync(projectId);
            var (owner, repo) = await GetRepoInfoAsync(projectId);

            var branches = await client.Repository.Branch.GetAll(owner, repo);
            var result = new List<GithubBranchDto>();

            foreach (var branch in branches)
            {
                var commit = await client.Repository.Commit.Get(owner, repo, branch.Commit.Sha);
                result.Add(new GithubBranchDto
                {
                    Name = branch.Name,
                    LastCommitSha = commit.Sha,
                    LastCommitMessage = commit.Commit.Message,
                    LastCommitAuthor = commit.Commit.Author?.Name ?? commit.Commit.Committer?.Name ?? "Unknown",
                    LastCommitDate = commit.Commit.Author?.Date.UtcDateTime ?? commit.Commit.Committer?.Date.UtcDateTime ?? DateTime.UtcNow
                });
            }

            return result;
        }

        public async Task<List<GithubPullRequestDto>> GetPullRequestsAsync(int projectId)
        {
            var client = await GetClientAsync(projectId);
            var (owner, repo) = await GetRepoInfoAsync(projectId);

            var prs = await client.PullRequest.GetAllForRepository(owner, repo, new PullRequestRequest { State = ItemStateFilter.All });

            return prs.Select(pr => new GithubPullRequestDto
            {
                Number = pr.Number,
                Title = pr.Title,
                State = pr.State.StringValue,
                HtmlUrl = pr.HtmlUrl,
                Author = pr.User.Login,
                CreatedAt = pr.CreatedAt.UtcDateTime,
                MergedAt = pr.MergedAt?.UtcDateTime,
                ClosedAt = pr.ClosedAt?.UtcDateTime
            }).ToList();
        }

        public async Task<List<GithubCommitDto>> GetCommitsAsync(int projectId)
        {
            var client = await GetClientAsync(projectId);
            var (owner, repo) = await GetRepoInfoAsync(projectId);

            var commits = await client.Repository.Commit.GetAll(owner, repo);

			var result = new List<GithubCommitDto>();

			foreach (var c in commits)
			{
				var detail = await client.Repository.Commit.Get(owner, repo, c.Sha);

				result.Add(new GithubCommitDto
				{
					Sha = c.Sha,
					Message = c.Commit.Message,
          AuthorLogin = c.Author?.Login,
					AuthorName = c.Commit.Author?.Name ?? c.Commit.Committer?.Name ?? c.Author?.Login ?? "Unknown",
					AuthorEmail = c.Commit.Author?.Email ?? c.Commit.Committer?.Email ?? "",
					Date = c.Commit.Author?.Date.UtcDateTime ?? c.Commit.Committer?.Date.UtcDateTime ?? DateTime.UtcNow,
					HtmlUrl = c.HtmlUrl,

					Additions = detail.Stats?.Additions ?? 0,
					Deletions = detail.Stats?.Deletions ?? 0,
					ChangedFiles = detail.Files?.Count ?? 0
				});
			}
			return result;
		}

        public async Task ValidateConnectionAsync(string apiToken, string repoOwner, string repoName)
        {
            try
            {
                var client = new GitHubClient(new ProductHeaderValue("JGMS"));
                client.Credentials = new Credentials(apiToken);

                var repo = await client.Repository.Get(repoOwner, repoName);

                if (repo == null)
                    throw new Exception($"Repository '{repoOwner}/{repoName}' not found.");
            }
            catch (Octokit.AuthorizationException)
            {
                throw new Exception("Invalid GitHub API token. Please check your token and try again.");
            }
            catch (Octokit.NotFoundException)
            {
                throw new Exception($"Repository '{repoOwner}/{repoName}' does not exist or is not accessible with the provided token.");
            }
            catch (Octokit.RateLimitExceededException)
            {
                throw new Exception("GitHub API rate limit exceeded. Please try again later.");
            }
            catch (Exception ex) when (!(ex.Message.StartsWith("Invalid") || ex.Message.StartsWith("Repository") || ex.Message.StartsWith("GitHub")))
            {
                throw new Exception($"Failed to connect to GitHub: {ex.Message}");
            }
        }
    }
}
