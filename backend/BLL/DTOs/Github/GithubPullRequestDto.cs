using System;

namespace BLL.DTOs.Github
{
    public class GithubPullRequestDto
    {
        public int Number { get; set; }
        public string Title { get; set; } = null!;
        public string State { get; set; } = null!;
        public string HtmlUrl { get; set; } = null!;
        public string Author { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime? MergedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
    }
}
