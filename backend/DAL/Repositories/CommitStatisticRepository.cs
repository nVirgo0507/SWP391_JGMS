using DAL.Models;
using DAL.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repositories
{
    public class CommitStatisticRepository : ICommitStatisticRepository
    {
        private readonly JgmsContext _context;

        public CommitStatisticRepository(JgmsContext context)
        {
            _context = context;
        }

        public async System.Threading.Tasks.Task<List<CommitStatistic>> GetLatestByProjectIdAsync(int projectId)
        {
            var rows = await _context.CommitStatistics
                .Include(s => s.User)
                .Where(s => s.ProjectId == projectId)
                .OrderByDescending(s => s.PeriodEnd)
                .ThenByDescending(s => s.UpdatedAt)
                .ToListAsync();

            return rows
                .GroupBy(s => s.UserId)
                .Select(g => g.First())
                .ToList();
        }

        public async System.Threading.Tasks.Task<List<CommitStatistic>> GetLatestByUserIdAsync(int userId)
        {
            var rows = await _context.CommitStatistics
                .Include(s => s.Project)
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.PeriodEnd)
                .ThenByDescending(s => s.UpdatedAt)
                .ToListAsync();

            return rows
                .GroupBy(s => s.ProjectId)
                .Select(g => g.First())
                .ToList();
        }

        public async System.Threading.Tasks.Task<CommitStatistic?> GetLatestByUserAndProjectIdAsync(int userId, int projectId)
        {
            return await _context.CommitStatistics
                .Include(s => s.User)
                .Include(s => s.Project)
                .Where(s => s.UserId == userId && s.ProjectId == projectId)
                .OrderByDescending(s => s.PeriodEnd)
                .ThenByDescending(s => s.UpdatedAt)
                .FirstOrDefaultAsync();
        }

        public async System.Threading.Tasks.Task RecalculateProjectStatisticsAsync(int projectId)
        {
            var commits = await _context.Commits
                .Where(c => c.ProjectId == projectId)
                .ToListAsync();

            var existingRows = await _context.CommitStatistics
                .Where(s => s.ProjectId == projectId)
                .ToListAsync();

            if (existingRows.Any())
            {
                _context.CommitStatistics.RemoveRange(existingRows);
            }

            if (!commits.Any())
            {
                await _context.SaveChangesAsync();
                return;
            }

            var now = DateTime.UtcNow;
            var periodStart = DateOnly.FromDateTime(commits.Min(c => c.CommitDate).Date);
            var periodEnd = DateOnly.FromDateTime(commits.Max(c => c.CommitDate).Date);

            var newRows = commits
                .GroupBy(c => c.UserId)
                .Select(g =>
                {
                    var userCommits = g.ToList();
                    var firstDate = userCommits.Min(c => c.CommitDate).Date;
                    var lastDate = userCommits.Max(c => c.CommitDate).Date;
                    var days = Math.Max(1, (lastDate - firstDate).TotalDays + 1);

                    return new CommitStatistic
                    {
                        ProjectId = projectId,
                        UserId = g.Key,
                        PeriodStart = periodStart,
                        PeriodEnd = periodEnd,
                        TotalCommits = userCommits.Count,
                        TotalAdditions = userCommits.Sum(c => c.Additions ?? 0),
                        TotalDeletions = userCommits.Sum(c => c.Deletions ?? 0),
                        TotalChangedFiles = userCommits.Sum(c => c.ChangedFiles ?? 0),
                        CommitFrequency = Math.Round((decimal)userCommits.Count / (decimal)days, 2),
                        AvgCommitSize = userCommits.Any()
                            ? (int)Math.Round(userCommits.Average(c => (c.Additions ?? 0) + (c.Deletions ?? 0)))
                            : 0,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                })
                .ToList();

            await _context.CommitStatistics.AddRangeAsync(newRows);
            await _context.SaveChangesAsync();
        }
    }
}


