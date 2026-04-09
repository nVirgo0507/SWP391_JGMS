using DAL.Models;

namespace DAL.Repositories.Interface
{
    public interface ICommitStatisticRepository
    {
        System.Threading.Tasks.Task<List<CommitStatistic>> GetByProjectIdAsync(int projectId);
        System.Threading.Tasks.Task<List<CommitStatistic>> GetByUserIdAsync(int userId);
        System.Threading.Tasks.Task<CommitStatistic?> GetByUserAndProjectIdAsync(int userId, int projectId);
        System.Threading.Tasks.Task<CommitStatistic?> GetByUserProjectAndPeriodAsync(int userId, int projectId, DateOnly start, DateOnly end);
        System.Threading.Tasks.Task AddAsync(CommitStatistic statistic);
        System.Threading.Tasks.Task UpdateAsync(CommitStatistic statistic);

        System.Threading.Tasks.Task<List<CommitStatistic>> GetLatestByProjectIdAsync(int projectId);
        System.Threading.Tasks.Task<List<CommitStatistic>> GetLatestByUserIdAsync(int userId);
        System.Threading.Tasks.Task<CommitStatistic?> GetLatestByUserAndProjectIdAsync(int userId, int projectId);
        System.Threading.Tasks.Task RecalculateProjectStatisticsAsync(int projectId);
    }
}

