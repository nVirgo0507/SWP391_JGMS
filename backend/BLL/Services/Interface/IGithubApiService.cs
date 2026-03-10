using BLL.DTOs.Github;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IGithubApiService
    {
        Task<List<GithubBranchDto>> GetBranchesAsync(int projectId);
        Task<List<GithubPullRequestDto>> GetPullRequestsAsync(int projectId);
        Task<List<GithubCommitDto>> GetCommitsAsync(int projectId);

        /// <summary>
        /// Validates that the provided token can access the given repository.
        /// Throws an exception with a descriptive message if the connection fails.
        /// </summary>
        Task ValidateConnectionAsync(string apiToken, string repoOwner, string repoName);
    }
}
