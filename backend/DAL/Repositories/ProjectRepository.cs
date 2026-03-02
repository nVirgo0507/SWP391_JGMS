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

        public async Task<(bool canDelete, string? reason)> CanDeleteProjectAsync(int projectId)
        {
            if (await _context.Requirements.AnyAsync(r => r.ProjectId == projectId))
                return (false, "Project has associated requirements. Please delete them first.");

            if (await _context.SrsDocuments.AnyAsync(s => s.ProjectId == projectId))
                return (false, "Project has associated SRS documents. Please delete them first.");

            if (await _context.ProgressReports.AnyAsync(r => r.ProjectId == projectId))
                return (false, "Project has associated progress reports. Please delete them first.");

            if (await _context.Commits.AnyAsync(c => c.ProjectId == projectId))
                return (false, "Project has associated commit records and cannot be deleted.");

            if (await _context.CommitStatistics.AnyAsync(c => c.ProjectId == projectId))
                return (false, "Project has associated commit statistics and cannot be deleted.");

            if (await _context.GithubCommits.AnyAsync(c => c.ProjectId == projectId))
                return (false, "Project has associated GitHub commits and cannot be deleted.");

            if (await _context.GithubIntegrations.AnyAsync(g => g.ProjectId == projectId))
                return (false, "Project has an active GitHub integration. Please remove it first.");

            if (await _context.JiraIntegrations.AnyAsync(j => j.ProjectId == projectId))
                return (false, "Project has an active Jira integration. Please remove it first.");

            if (await _context.JiraIssues.AnyAsync(j => j.ProjectId == projectId))
                return (false, "Project has associated Jira issues and cannot be deleted.");

            if (await _context.PersonalTaskStatistics.AnyAsync(p => p.ProjectId == projectId))
                return (false, "Project has associated personal task statistics and cannot be deleted.");

            if (await _context.TeamCommitSummaries.AnyAsync(t => t.ProjectId == projectId))
                return (false, "Project has associated team commit summaries and cannot be deleted.");

            return (true, null);
        }
    }
}


