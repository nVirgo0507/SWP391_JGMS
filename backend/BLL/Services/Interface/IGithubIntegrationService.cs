using BLL.DTOs.Github;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IGithubIntegrationService
    {
        Task SyncCommitsAsync(int projectId);
        Task ProcessWebhookEventAsync(string eventType, string payload);
    }
}
