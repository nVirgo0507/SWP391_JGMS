using BLL.DTOs.Admin;
using BLL.DTOs.Jira;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

namespace BLL.Services.Interface
{
    public interface ITeamLeaderSrsService
    {
        #region SRS Document Management

        /// <summary>
        /// Get all SRS documents for the leader's group project
        /// </summary>
        Task<List<SrsDocumentResponseDTO>> GetGroupSrsDocumentsAsync(int userId, int groupId);

        /// <summary>
        /// Get a single SRS document by ID
        /// </summary>
        Task<SrsDocumentResponseDTO?> GetGroupSrsDocumentAsync(int userId, int groupId, int documentId);

        /// <summary>
        /// Generate an SRS document from existing requirements.
        /// Creates the header record and snapshots each requirement into SRS_INCLUDED_REQUIREMENT.
        /// </summary>
        Task<SrsDocumentResponseDTO> GenerateSrsDocumentAsync(int userId, int groupId, CreateSrsDocumentDTO dto);

        /// <summary>
        /// Update SRS document metadata (title, version, intro, scope, status)
        /// </summary>
        Task<SrsDocumentResponseDTO> UpdateSrsDocumentAsync(int userId, int groupId, int documentId, UpdateSrsDocumentDTO dto);

        /// <summary>
        /// Generate a downloadable HTML file of the SRS document
        /// </summary>
        Task<(byte[] content, string fileName)> DownloadSrsDocumentAsync(int userId, int groupId, int documentId);

        /// <summary>
        /// Generate a downloadable Word-compatible (.doc) file of the SRS document
        /// </summary>
        Task<(byte[] content, string fileName)> DownloadSrsDocumentAsDocAsync(int userId, int groupId, int documentId);

        /// <summary>
        /// Regenerate the requirement snapshot of an existing SRS document without creating a new version.
        /// Replaces all previously included requirements with the newly selected set.
        /// Also updates the Scope section to reflect the new requirement count.
        /// </summary>
        Task<SrsDocumentResponseDTO> RegenerateSrsDocumentAsync(int userId, int groupId, int documentId, RegenerateSrsDocumentDTO dto);

        /// <summary>
        /// Automatically generate SRS document sections using AI based on project requirements.
        /// </summary>
        Task<BLL.DTOs.Student.AiSrsResponseDTO> GenerateAiSrsContentAsync(int userId, int groupId, BLL.DTOs.Student.AiSrsRequestDTO dto);

        #endregion
    }
}
