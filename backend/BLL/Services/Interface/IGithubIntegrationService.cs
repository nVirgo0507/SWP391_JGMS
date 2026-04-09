using BLL.DTOs.Github;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IGithubIntegrationService
    {
        Task<GithubIntegrationResponseDto?> GetIntegrationAsync(int projectId);
        Task<GithubSyncSummaryDto> SyncCommitsAsync(int projectId, bool forceFullResync = false);
        Task SyncCommitStatisticsAsync(int projectId);
        Task ProcessWebhookEventAsync(string eventType, string payload);
    }
}
