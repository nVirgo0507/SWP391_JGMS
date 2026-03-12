using DAL.Models;
using System.Collections.Generic;
using SystemTask = System.Threading.Tasks.Task;

namespace DAL.Repositories.Interface
{
    public interface IGithubIntegrationRepository
    {
        System.Threading.Tasks.Task<GithubIntegration?> GetByProjectIdAsync(int projectId);
        System.Threading.Tasks.Task<List<GithubIntegration>> GetAllAsync();
        SystemTask AddAsync(GithubIntegration integration);
        SystemTask UpdateAsync(GithubIntegration integration);
        SystemTask UpdateAllAsync(List<GithubIntegration> integrations);
        SystemTask DeleteAsync(int integrationId);
    }
}
