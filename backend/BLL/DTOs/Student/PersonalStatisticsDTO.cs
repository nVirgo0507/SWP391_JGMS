namespace BLL.DTOs.Student
{
    /// <summary>
    /// DTO for personal task statistics
    /// </summary>
    public class PersonalStatisticsDTO
    {
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int InProgressTasks { get; set; }
        public int OverdueTasks { get; set; }
        public decimal CompletionRate { get; set; }
        public DateTime? LastCalculated { get; set; }
        
        // Commit statistics
        public int TotalCommits { get; set; }
        public int TotalAdditions { get; set; }
        public int TotalDeletions { get; set; }
        public int TotalChangedFiles { get; set; }
        public DateTime? LastCommitDate { get; set; }
    }
}
