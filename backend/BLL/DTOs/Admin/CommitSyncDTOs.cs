using System;

namespace BLL.DTOs.Admin
{
    /// <summary>
    /// Result of a commit sync operation.
    /// </summary>
    public class CommitSyncResultDTO
    {
        public int ProjectId { get; set; }
        public int RawCommitsScanned { get; set; }
        public int CommitsLinked { get; set; }
        public int CommitsSkippedNoUserMatch { get; set; }
        public int CommitsAlreadyLinked { get; set; }
        public DateTime SyncedAt { get; set; }
    }
}
