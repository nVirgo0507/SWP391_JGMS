using DAL.Models;
using DAL.Repositories.Interface;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using SystemTask = System.Threading.Tasks.Task;

namespace DAL.Repositories
{
    public class GithubIntegrationRepository : IGithubIntegrationRepository
    {
        private readonly JgmsContext _context;

        public GithubIntegrationRepository(JgmsContext context)
        {
            _context = context;
        }

        public async System.Threading.Tasks.Task<GithubIntegration?> GetByProjectIdAsync(int projectId)
        {
            return await _context.GithubIntegrations
                .FirstOrDefaultAsync(g => g.ProjectId == projectId);
        }

        public async System.Threading.Tasks.Task<List<GithubIntegration>> GetAllAsync()
        {
            return await _context.GithubIntegrations.ToListAsync();
        }

        public async SystemTask AddAsync(GithubIntegration integration)
        {
            await _context.GithubIntegrations.AddAsync(integration);
            await _context.SaveChangesAsync();
        }

        public async SystemTask UpdateAsync(GithubIntegration integration)
        {
            _context.GithubIntegrations.Update(integration);
            await _context.SaveChangesAsync();
        }

        public async SystemTask UpdateAllAsync(List<GithubIntegration> integrations)
        {
            _context.GithubIntegrations.UpdateRange(integrations);
            await _context.SaveChangesAsync();
        }

        public async SystemTask DeleteAsync(int integrationId)
        {
            var integration = await _context.GithubIntegrations.FindAsync(integrationId);
            if (integration != null)
            {
                _context.GithubIntegrations.Remove(integration);
                await _context.SaveChangesAsync();
            }
        }
    }
}
