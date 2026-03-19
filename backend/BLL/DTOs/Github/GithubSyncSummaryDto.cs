namespace BLL.DTOs.Github
{
    /// <summary>
    /// Lightweight sync result returned by the GitHub sync endpoint.
    /// </summary>
    public class GithubSyncSummaryDto
    {
        public int ProjectId { get; set; }
        public bool IncrementalSync { get; set; }
        public DateTime? Since { get; set; }
        public int GithubFetched { get; set; }
        public int DuplicateShaSkipped { get; set; }
        public int NewRawGithubCommits { get; set; }
        public int LocalCommitsRecovered { get; set; }
        public int UnmatchedLocalCommits { get; set; }
        public long ElapsedMilliseconds { get; set; }
    }
}

