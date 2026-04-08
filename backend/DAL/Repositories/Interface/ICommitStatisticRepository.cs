using DAL.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DAL.Repositories.Interface
{
    public interface ICommitStatisticRepository
    {
        Task<List<CommitStatistic>> GetByProjectIdAsync(int projectId);
        Task<CommitStatistic?> GetByUserAndProjectIdAsync(int userId, int projectId);
        Task<CommitStatistic?> GetByUserProjectAndPeriodAsync(int userId, int projectId, DateOnly start, DateOnly end);
        System.Threading.Tasks.Task AddAsync(CommitStatistic statistic);
        System.Threading.Tasks.Task UpdateAsync(CommitStatistic statistic);
        Task<List<CommitStatistic>> GetByUserIdAsync(int userId);
    }
}
