using DAL.Models;
using DAL.Repositories.Interface;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DAL.Repositories
{
    public class CommitStatisticRepository : ICommitStatisticRepository
    {
        private readonly JgmsContext _context;

        public CommitStatisticRepository(JgmsContext context)
        {
            _context = context;
        }

        public async Task<List<CommitStatistic>> GetByProjectIdAsync(int projectId)
        {
            return await _context.CommitStatistics
                .Where(s => s.ProjectId == projectId)
                .ToListAsync();
        }

        public async Task<CommitStatistic?> GetByUserAndProjectIdAsync(int userId, int projectId)
        {
            return await _context.CommitStatistics
                .FirstOrDefaultAsync(s => s.UserId == userId && s.ProjectId == projectId);
        }

        public async Task<CommitStatistic?> GetByUserProjectAndPeriodAsync(int userId, int projectId, DateOnly start, DateOnly end)
        {
            return await _context.CommitStatistics
                .FirstOrDefaultAsync(s => s.UserId == userId && s.ProjectId == projectId && s.PeriodStart == start && s.PeriodEnd == end);
        }

        public async Task<List<CommitStatistic>> GetByUserIdAsync(int userId)
        {
            return await _context.CommitStatistics
                .Where(s => s.UserId == userId)
                .ToListAsync();
        }

        public async System.Threading.Tasks.Task AddAsync(CommitStatistic statistic)
        {
            await _context.CommitStatistics.AddAsync(statistic);
            await _context.SaveChangesAsync();
        }

        public async System.Threading.Tasks.Task UpdateAsync(CommitStatistic statistic)
        {
            _context.CommitStatistics.Update(statistic);
            await _context.SaveChangesAsync();
        }
    }
}
