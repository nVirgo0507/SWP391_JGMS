using BLL.DTOs.Admin;
using BLL.DTOs.Jira;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

namespace BLL.Services.Interface
{
    public interface ITeamLeaderRequirementService
    {
        #region Requirements Management

        /// <summary>
        /// BR-055: Get all requirements for the leader's group project
        /// Validates that user is leader of the group
        /// </summary>
        Task<List<RequirementResponseDTO>> GetGroupRequirementsAsync(int userId, int groupId);

        /// <summary>
        /// BR-055: Create a requirement for the leader's group
        /// Validates that user is leader of the group
        /// </summary>
        Task<RequirementResponseDTO> CreateRequirementAsync(int userId, int groupId, CreateRequirementDTO dto);

        /// <summary>
        /// BR-055: Update a requirement for the leader's group
        /// Validates that user is leader of the group
        /// </summary>
        Task<RequirementResponseDTO> UpdateRequirementAsync(int userId, int groupId, int requirementId, UpdateRequirementDTO dto);

        /// <summary>
        /// BR-055: Delete a requirement for the leader's group
        /// Validates that user is leader of the group
        /// </summary>
        Task DeleteRequirementAsync(int userId, int groupId, int requirementId);

        /// <summary>
        /// BR-055: Reorder/organise requirements hierarchy (Epic → Story → Task)
        /// Returns the requirements in the requested order
        /// </summary>
        Task<List<RequirementResponseDTO>> ReorderRequirementsAsync(int userId, int groupId, ReorderRequirementsDTO dto);

        /// <summary>
        /// BR-055: Bulk-import all synced Jira issues that don't already have a linked requirement.
        /// Skips issues that are already linked. Returns a summary of what was imported.
        /// </summary>
        Task<BulkImportFromJiraResultDTO> ImportRequirementsFromJiraAsync(int userId, int groupId);

        #endregion
    }
}
