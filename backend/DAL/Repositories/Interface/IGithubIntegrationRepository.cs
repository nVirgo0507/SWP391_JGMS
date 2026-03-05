using DAL.Models;
using System.Threading.Tasks;

namespace DAL.Repositories.Interface
{
    public interface IGithubIntegrationRepository
    {
        Task<GithubIntegration?> GetByProjectIdAsync(int projectId);
        System.Threading.Tasks.Task AddAsync(GithubIntegration integration);
        System.Threading.Tasks.Task UpdateAsync(GithubIntegration integration);
        System.Threading.Tasks.Task DeleteAsync(int integrationId);
    }
}
