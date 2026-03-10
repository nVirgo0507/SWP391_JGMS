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
    }
}
