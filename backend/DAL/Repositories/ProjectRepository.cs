using DAL.Data;
using DAL.Models;
using DAL.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repositories
{
    public class ProjectRepository : IProjectRepository
    {
        private readonly JgmsContext _context;

        public ProjectRepository(JgmsContext context)
        {
            _context = context;
        }

        public async Task<Project?> GetByIdAsync(int projectId)
        {
            return await _context.Projects
                .Include(p => p.Group)
                .ThenInclude(g => g.Lecturer)
                .Include(p => p.Group)
                .ThenInclude(g => g.Leader)
                .FirstOrDefaultAsync(p => p.ProjectId == projectId);
        }

        public async Task<Project?> GetByGroupIdAsync(int groupId)
        {
            return await _context.Projects
                .Include(p => p.Group)
                .FirstOrDefaultAsync(p => p.GroupId == groupId);
        }

        public async Task<List<Project>> GetAllAsync()
        {
            return await _context.Projects
                .Include(p => p.Group)
                .ToListAsync();
        }

        public async System.Threading.Tasks.Task AddAsync(Project project)
        {
            project.CreatedAt = DateTime.UtcNow;
            project.UpdatedAt = DateTime.UtcNow;
            await _context.Projects.AddAsync(project);
            await _context.SaveChangesAsync();
        }

        public async System.Threading.Tasks.Task UpdateAsync(Project project)
        {
            project.UpdatedAt = DateTime.UtcNow;
            _context.Projects.Update(project);
            await _context.SaveChangesAsync();
        }

        public async System.Threading.Tasks.Task DeleteAsync(int projectId)
        {
            var project = await GetByIdAsync(projectId);
            if (project != null)
            {
                _context.Projects.Remove(project);
                await _context.SaveChangesAsync();
            }
        }
    }
}


