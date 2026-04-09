using DAL.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DAL.Repositories.Interface
{
    public interface IGithubCommitRepository
    {
        Task<List<GithubCommit>> GetCommitsByProjectIdAsync(int projectId);
        Task<GithubCommit?> GetByShaAsync(string sha);
        Task<bool> CommitExistsAsync(string sha);
        System.Threading.Tasks.Task AddAsync(GithubCommit commit);
        System.Threading.Tasks.Task AddRangeAsync(IEnumerable<GithubCommit> commits);
        Task<int> GetCountByProjectIdAsync(int projectId);
    }
}
