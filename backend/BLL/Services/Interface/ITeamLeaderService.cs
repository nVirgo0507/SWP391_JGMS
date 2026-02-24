using BLL.DTOs.Admin;
using BLL.DTOs.Jira;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    /// <summary>
    /// Team Leader Service Interface
    /// BR-055: Team Leader Group-Scoped Access - Team leaders can only manage their own group's project
    /// Validation: Check user is leader of the group via GROUP_MEMBER.is_leader
    /// Error Message: "Access denied. You are not the leader of this group."
    /// </summary>
    public interface ITeamLeaderService
    {
        #region Project Management

        /// <summary>
        /// BR-055: Get project details for the leader's group
        /// Validates that user is leader of the group
        /// </summary>
        Task<ProjectResponseDTO?> GetGroupProjectAsync(int userId, int groupId);

        #endregion

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

        #endregion

        #region Tasks Management

        /// <summary>
        /// BR-055: Get all tasks for the leader's group
        /// Validates that user is leader of the group
        /// </summary>
        Task<List<TaskResponseDTO>> GetGroupTasksAsync(int userId, int groupId);

        /// <summary>
        /// BR-055: Create a task for the leader's group
        /// Validates that user is leader of the group
        /// </summary>
        Task<TaskResponseDTO> CreateTaskAsync(int userId, int groupId, CreateTaskDTO dto);

        /// <summary>
        /// BR-055: Create a task pre-populated from a synced Jira issue key (e.g. "SWP391-5").
        /// Validates that user is leader of the group and that the issue belongs to the group's project.
        /// Prevents duplicate tasks from being created for the same Jira issue.
        /// </summary>
        Task<TaskResponseDTO> CreateTaskFromJiraIssueAsync(int userId, int groupId, CreateTaskFromJiraIssueDTO dto);

        /// <summary>
        /// BR-055: Update a task for the leader's group
        /// Validates that user is leader of the group
        /// </summary>
        Task<TaskResponseDTO> UpdateTaskAsync(int userId, int groupId, int taskId, UpdateTaskDTO dto);

        /// <summary>BR-055: Assign task to team member</summary>
        Task AssignTaskAsync(int userId, int groupId, int taskId, int memberId);

        /// <summary>BR-055: Delete a task and optionally remove from Jira</summary>
        Task DeleteTaskAsync(int userId, int groupId, int taskId);

        /// <summary>BR-055: Move a task's linked Jira issue to a sprint (or backlog if sprintId == 0)</summary>
        Task<TaskResponseDTO> MoveTaskToSprintAsync(int userId, int groupId, int taskId, int sprintId);

        /// <summary>BR-055: Link a task to a requirement locally and via Jira issue link</summary>
        Task<TaskResponseDTO> LinkTaskToRequirementAsync(int userId, int groupId, int taskId, int requirementId);

        /// <summary>
        /// BR-055: Push all local task and requirement changes to Jira in bulk.
        /// Syncs title, description, status, priority, and assignee for every
        /// locally-linked item. Returns a summary of what succeeded and what failed.
        /// </summary>
        Task<JiraPushSyncResultDTO> SyncToJiraAsync(int userId, int groupId);

        #endregion

        #region SRS Document Management

        /// <summary>
        /// BR-055: Get SRS document for the leader's group
        /// Validates that user is leader of the group
        /// </summary>
        Task<SrsDocumentResponseDTO?> GetGroupSrsDocumentAsync(int userId, int groupId);

        /// <summary>
        /// BR-055: Create SRS document for the leader's group
        /// Validates that user is leader of the group
        /// </summary>
        Task<SrsDocumentResponseDTO> CreateSrsDocumentAsync(int userId, int groupId, CreateSrsDocumentDTO dto);

        /// <summary>
        /// BR-055: Update SRS document for the leader's group
        /// Validates that user is leader of the group
        /// </summary>
        Task<SrsDocumentResponseDTO> UpdateSrsDocumentAsync(int userId, int groupId, int srsId, UpdateSrsDocumentDTO dto);

        #endregion
    }
}
