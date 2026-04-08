using DAL.Models;
using Task = System.Threading.Tasks.Task;

namespace DAL.Repositories.Interface
{
    public interface ICommitRepository
    {
        Task<List<Commit>> GetCommitsByUserIdAsync(int userId);
        Task<List<Commit>> GetCommitsByUserIdAndProjectIdAsync(int userId, int projectId);
        Task<List<Commit>> GetCommitsByProjectIdAsync(int projectId);
        Task<Commit?> GetByIdAsync(int commitId);
        Task<bool> ExistsByGithubCommitIdAsync(int githubCommitId);
        Task<List<Commit>> GetCommitsByProjectIdAsync(int projectId);
        System.Threading.Tasks.Task AddAsync(Commit commit);
        System.Threading.Tasks.Task AddRangeAsync(IEnumerable<Commit> commits);
    }
}
