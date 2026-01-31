using DAL.Models;
using DAL.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repositories
{
    public class TaskRepository : ITaskRepository
    {
        private readonly JgmsContext _context;

        public TaskRepository(JgmsContext context)
        {
            _context = context;
        }

        public async System.Threading.Tasks.Task<List<DAL.Models.Task>> GetTasksByUserIdAsync(int userId)
        {
            return await _context.Tasks
                .Include(t => t.AssignedToNavigation)
                .Include(t => t.JiraIssue)
                .Include(t => t.Requirement)
                .Where(t => t.AssignedTo == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
        }

        public async System.Threading.Tasks.Task<DAL.Models.Task?> GetByIdAsync(int taskId)
        {
            return await _context.Tasks
                .Include(t => t.AssignedToNavigation)
                .Include(t => t.JiraIssue)
                .Include(t => t.Requirement)
                .FirstOrDefaultAsync(t => t.TaskId == taskId);
        }

        public async System.Threading.Tasks.Task UpdateAsync(DAL.Models.Task task)
        {
            task.UpdatedAt = DateTime.UtcNow;
            _context.Tasks.Update(task);
            await _context.SaveChangesAsync();
        }

        public async System.Threading.Tasks.Task<List<DAL.Models.Task>> GetTasksByProjectIdAsync(int projectId)
        {
            return await _context.Tasks
                .Include(t => t.Requirement)
                .Where(t => t.Requirement != null && t.Requirement.ProjectId == projectId)
                .ToListAsync();
        }

        public async System.Threading.Tasks.Task<List<DAL.Models.Task>> GetOverdueTasksByUserIdAsync(int userId)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            return await _context.Tasks
                .Where(t => t.AssignedTo == userId
                    && t.DueDate.HasValue
                    && t.DueDate < today
                    && !t.CompletedAt.HasValue)
                .ToListAsync();
        }

        public async System.Threading.Tasks.Task<int> CountTasksByStatusAsync(int userId, string status)
        {
            // Parse status string to enum and count tasks by actual Status field
            var statusLower = status.ToLower();

            if (statusLower == "completed" || statusLower == "done")
            {
                return await _context.Tasks
                    .Where(t => t.AssignedTo == userId && t.Status == DAL.Models.TaskStatus.done)
                    .CountAsync();
            }
            else if (statusLower == "in_progress" || statusLower == "in progress")
            {
                return await _context.Tasks
                    .Where(t => t.AssignedTo == userId && t.Status == DAL.Models.TaskStatus.in_progress)
                    .CountAsync();
            }
            else if (statusLower == "todo" || statusLower == "to do")
            {
                return await _context.Tasks
                    .Where(t => t.AssignedTo == userId && t.Status == DAL.Models.TaskStatus.todo)
                    .CountAsync();
            }

            // Default: count all tasks for user
            return await _context.Tasks
                .Where(t => t.AssignedTo == userId)
                .CountAsync();
        }
    }
}
