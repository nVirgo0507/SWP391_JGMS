using System;

namespace BLL.DTOs.Github
{
    public class GithubBranchDto
    {
        public string Name { get; set; } = null!;
        public string LastCommitSha { get; set; } = null!;
        public string LastCommitMessage { get; set; } = null!;
        public string LastCommitAuthor { get; set; } = null!;
        public DateTime LastCommitDate { get; set; }
    }
}
