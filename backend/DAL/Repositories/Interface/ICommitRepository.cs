using DAL.Models;

namespace DAL.Repositories.Interface
{
    public interface ICommitRepository
    {
        Task<List<Commit>> GetCommitsByUserIdAsync(int userId);
        Task<List<Commit>> GetCommitsByUserIdAndProjectIdAsync(int userId, int projectId);
        Task<Commit?> GetByIdAsync(int commitId);
        System.Threading.Tasks.Task AddAsync(Commit commit);
        System.Threading.Tasks.Task AddRangeAsync(IEnumerable<Commit> commits);
    }
}
