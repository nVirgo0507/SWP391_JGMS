namespace BLL.DTOs.Student
{
    /// <summary>
    /// DTO for SRS document information
    /// </summary>
    public class SrsDocumentDTO
    {
        public int DocumentId { get; set; }
        public int ProjectId { get; set; }
        public string Version { get; set; } = null!;
        public string DocumentTitle { get; set; } = null!;
        public string? Introduction { get; set; }
        public string? Scope { get; set; }
        public string? FilePath { get; set; }
        public int GeneratedBy { get; set; }
        public string? GeneratedByName { get; set; }
        public DateTime? GeneratedAt { get; set; }
    }
}
