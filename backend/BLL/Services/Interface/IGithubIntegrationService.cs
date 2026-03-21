using BLL.DTOs.Github;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IGithubIntegrationService
    {
        Task<GithubSyncSummaryDto> SyncCommitsAsync(int projectId, bool forceFullResync = false);
        Task ProcessWebhookEventAsync(string eventType, string payload);
    }
}
