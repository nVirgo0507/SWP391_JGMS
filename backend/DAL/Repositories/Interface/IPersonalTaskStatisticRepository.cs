using DAL.Models;

namespace DAL.Repositories.Interface
{
    public interface IPersonalTaskStatisticRepository
    {
        Task<PersonalTaskStatistic?> GetByUserIdAndProjectIdAsync(int userId, int projectId);
        Task<List<PersonalTaskStatistic>> GetByUserIdAsync(int userId);
    }
}
