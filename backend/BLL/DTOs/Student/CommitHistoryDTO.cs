namespace BLL.DTOs.Student
{
    /// <summary>
    /// DTO for commit history information
    /// </summary>
    public class CommitHistoryDTO
    {
        public int CommitId { get; set; }
        public string? CommitMessage { get; set; }
        public int? Additions { get; set; }
        public int? Deletions { get; set; }
        public int? ChangedFiles { get; set; }
        public DateTime CommitDate { get; set; }
        public int ProjectId { get; set; }
        public string? ProjectName { get; set; }
    }
}
