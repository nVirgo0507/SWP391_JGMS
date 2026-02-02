using DAL.Models;
using DAL.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repositories
{
    public class PersonalTaskStatisticRepository : IPersonalTaskStatisticRepository
    {
        private readonly JgmsContext _context;

        public PersonalTaskStatisticRepository(JgmsContext context)
        {
            _context = context;
        }

        public async Task<PersonalTaskStatistic?> GetByUserIdAndProjectIdAsync(int userId, int projectId)
        {
            return await _context.PersonalTaskStatistics
                .Include(p => p.Project)
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.UserId == userId && p.ProjectId == projectId);
        }

        public async Task<List<PersonalTaskStatistic>> GetByUserIdAsync(int userId)
        {
            return await _context.PersonalTaskStatistics
                .Include(p => p.Project)
                .Where(p => p.UserId == userId)
                .ToListAsync();
        }
    }
}
