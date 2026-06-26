using BLL.DTOs.Admin;
using BLL.DTOs.Jira;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

namespace BLL.Services.Interface
{
    public interface ITeamLeaderProjectService
    {
        #region Project Management

        /// <summary>
        /// BR-055: Get project details for the leader's group
        /// Validates that user is leader of the group
        /// </summary>
        Task<ProjectResponseDTO?> GetGroupProjectAsync(int userId, int groupId);

        /// <summary>
        /// Get progress reports for the leader's group project.
        /// </summary>
        Task<List<ProgressReportResponseDTO>> GetGroupProgressReportsAsync(int userId, int groupId);

        /// <summary>
        /// Create a new progress report for the leader's group project.
        /// </summary>
        Task<ProgressReportResponseDTO> CreateProgressReportAsync(int userId, int groupId, CreateProgressReportDTO dto);

        /// <summary>
        /// Get guided template for creating progress reports in frontend forms.
        /// </summary>
        Task<ProgressReportTemplateDTO> GetGroupProgressReportTemplateAsync(int userId, int groupId);

        /// <summary>
        /// Export a progress report for the leader's group project.
        /// Supported formats: word, pdf.
        /// </summary>
        Task<(byte[] content, string fileName, string contentType)> ExportGroupProgressReportAsync(int userId, int groupId, int reportId, string format);

        /// <summary>
        /// Get commit statistics for all members of the leader's group.
        /// Leader only — validates leader membership.
        /// </summary>
        Task<GroupCommitStatisticsResponseDTO> GetGroupCommitStatisticsAsync(int userId, int groupId, DateOnly? startDate = null, DateOnly? endDate = null);

        #endregion
    }
}
