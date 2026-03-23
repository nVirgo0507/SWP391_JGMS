using BLL.DTOs.Admin;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    /// <summary>
    /// Syncs raw GitHub commits into the internal COMMIT table.
    /// BR-040: GitHub Username Matching Required - commits are only linked to users if github_username matches
    /// </summary>
    public interface IGithubSyncService
    {
        /// <summary>
        /// Sync raw GitHub commits for a project into the linked COMMIT table.
        /// Only links commits to users whose USER.github_username matches the raw commit author_username.
        /// </summary>
        Task<CommitSyncResultDTO> SyncCommitsAsync(int projectId);
        /// <summary>
        /// Import raw GitHub commits into the GITHUB_COMMIT table.
        /// BR-041: Enforces unique commit SHA for each imported commit.
        /// </summary>
        Task<CommitSyncResultDTO> ImportRawCommitsAsync(int projectId, List<DTOs.Admin.GithubRawCommitDTO> rawCommits);    }
}
