using System;

namespace BLL.DTOs.Github
{
    public class GithubCommitDto
    {
        public string Sha { get; set; } = null!;
        public string Message { get; set; } = null!;
        public string AuthorName { get; set; } = null!;
        public string AuthorEmail { get; set; } = null!;
        public DateTime Date { get; set; }
        public string HtmlUrl { get; set; } = null!;
        public int Additions { get; set; }
        public int Deletions { get; set; }
        public int ChangedFiles { get; set; }
    }
}
