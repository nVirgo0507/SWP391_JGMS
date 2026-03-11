namespace BLL.DTOs.Admin
{
    /// <summary>
    /// Result of a bulk add-students-to-group operation.
    /// Reports which identifiers succeeded and which failed (with reasons).
    /// </summary>
    public class BulkAddResult
    {
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public List<string> Added { get; set; } = new();
        public List<BulkAddFailure> Failures { get; set; } = new();
    }

    public class BulkAddFailure
    {
        public string Identifier { get; set; } = null!;
        public string Reason { get; set; } = null!;
    }
}

