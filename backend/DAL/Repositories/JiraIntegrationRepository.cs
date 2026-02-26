using DAL.Data;
using DAL.Models;
using DAL.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repositories
{
    /// <summary>
    /// Implementation of Jira Integration repository
    /// </summary>
    public class JiraIntegrationRepository : IJiraIntegrationRepository
    {
        private readonly JgmsContext _context;

        public JiraIntegrationRepository(JgmsContext context)
        {
            _context = context;
        }

        public async Task<JiraIntegration?> GetByProjectIdAsync(int projectId)
        {
            return await _context.JiraIntegrations
                .Include(j => j.Project)
                .ThenInclude(p => p.Group)
                .FirstOrDefaultAsync(j => j.ProjectId == projectId);
        }

        public async Task<JiraIntegration?> GetByIdAsync(int integrationId)
        {
            return await _context.JiraIntegrations
                .Include(j => j.Project)
                .FirstOrDefaultAsync(j => j.IntegrationId == integrationId);
        }

        public async System.Threading.Tasks.Task AddAsync(JiraIntegration integration)
        {
            integration.CreatedAt = DateTime.UtcNow;
            integration.UpdatedAt = DateTime.UtcNow;
            await _context.JiraIntegrations.AddAsync(integration);
            await _context.SaveChangesAsync();
        }

        public async System.Threading.Tasks.Task UpdateAsync(JiraIntegration integration)
        {
            integration.UpdatedAt = DateTime.UtcNow;
            _context.JiraIntegrations.Update(integration);
            await _context.SaveChangesAsync();
        }

        public async System.Threading.Tasks.Task DeleteAsync(int integrationId)
        {
            var integration = await GetByIdAsync(integrationId);
            if (integration != null)
            {
                _context.JiraIntegrations.Remove(integration);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> ExistsForProjectAsync(int projectId)
        {
            return await _context.JiraIntegrations
                .AnyAsync(j => j.ProjectId == projectId);
        }

        public async Task<List<JiraIntegration>> GetAllAsync()
        {
            return await _context.JiraIntegrations
                .Include(j => j.Project)
                .ThenInclude(p => p.Group)
                .ToListAsync();
        }
    }
}


