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

        public async System.Threading.Tasks.Task RecalculateForUserProjectAsync(int userId, int projectId)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

            var assignedTasks = await _context.Tasks
                .Where(t => t.AssignedTo == userId &&
                    ((t.Requirement != null && t.Requirement.ProjectId == projectId) ||
                     (t.JiraIssue != null && t.JiraIssue.ProjectId == projectId)))
                .ToListAsync();

            var totalTasks = assignedTasks.Count;
            var completedTasks = assignedTasks.Count(t => t.Status == DAL.Models.TaskStatus.done);
            var inProgressTasks = assignedTasks.Count(t => t.Status == DAL.Models.TaskStatus.in_progress);
            var overdueTasks = assignedTasks.Count(t =>
                t.DueDate.HasValue &&
                t.DueDate.Value < today &&
                t.Status != DAL.Models.TaskStatus.done);

            var completionRate = totalTasks == 0
                ? 0m
                : (decimal)completedTasks / totalTasks * 100m;

            var now = DateTime.UtcNow;
            var stat = await _context.PersonalTaskStatistics
                .FirstOrDefaultAsync(p => p.UserId == userId && p.ProjectId == projectId);

            if (stat == null)
            {
                stat = new PersonalTaskStatistic
                {
                    UserId = userId,
                    ProjectId = projectId,
                    CreatedAt = now
                };
                await _context.PersonalTaskStatistics.AddAsync(stat);
            }

            stat.TotalTasks = totalTasks;
            stat.CompletedTasks = completedTasks;
            stat.InProgressTasks = inProgressTasks;
            stat.OverdueTasks = overdueTasks;
            stat.CompletionRate = completionRate;
            stat.LastCalculated = now;
            stat.UpdatedAt = now;

            await _context.SaveChangesAsync();
        }
    }
}
