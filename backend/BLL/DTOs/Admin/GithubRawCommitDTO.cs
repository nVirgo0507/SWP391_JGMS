using System;

namespace BLL.DTOs.Admin
{
    /// <summary>
    /// DTO for importing raw GitHub commits during sync.
    /// </summary>
    public class GithubRawCommitDTO
    {
        public string CommitSha { get; set; } = null!;
        public string AuthorUsername { get; set; } = null!;
        public string? AuthorEmail { get; set; }
        public string? CommitMessage { get; set; }
        public int? Additions { get; set; }
        public int? Deletions { get; set; }
        public int? ChangedFiles { get; set; }
        public DateTime CommitDate { get; set; }
        public string? BranchName { get; set; }
    }
}
