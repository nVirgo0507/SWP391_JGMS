using DAL.Models;
using DAL.Repositories.Interface;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace DAL.Repositories
{
    public class GithubIntegrationRepository : IGithubIntegrationRepository
    {
        private readonly JgmsContext _context;

        public GithubIntegrationRepository(JgmsContext context)
        {
            _context = context;
        }

        public async Task<GithubIntegration?> GetByProjectIdAsync(int projectId)
        {
            return await _context.GithubIntegrations
                .FirstOrDefaultAsync(g => g.ProjectId == projectId);
        }

        public async System.Threading.Tasks.Task AddAsync(GithubIntegration integration)
        {
            await _context.GithubIntegrations.AddAsync(integration);
            await _context.SaveChangesAsync();
        }

        public async System.Threading.Tasks.Task UpdateAsync(GithubIntegration integration)
        {
            _context.GithubIntegrations.Update(integration);
            await _context.SaveChangesAsync();
        }

        public async System.Threading.Tasks.Task DeleteAsync(int integrationId)
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
