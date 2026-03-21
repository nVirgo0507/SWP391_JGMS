using DAL.Models;

namespace DAL.Repositories.Interface
{
    public interface ICommitStatisticRepository
    {
        System.Threading.Tasks.Task<List<CommitStatistic>> GetLatestByProjectIdAsync(int projectId);
        System.Threading.Tasks.Task<List<CommitStatistic>> GetLatestByUserIdAsync(int userId);
        System.Threading.Tasks.Task<CommitStatistic?> GetLatestByUserAndProjectIdAsync(int userId, int projectId);
        System.Threading.Tasks.Task RecalculateProjectStatisticsAsync(int projectId);
    }
}


