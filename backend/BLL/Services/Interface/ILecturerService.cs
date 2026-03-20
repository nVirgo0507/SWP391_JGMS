using BLL.DTOs.Admin;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface ILecturerService
    {
        Task<StudentGroupResponseDTO?> GetGroupByIdAsync(int lecturerId, int groupId);

        Task<List<StudentGroupResponseDTO>> GetMyGroupsAsync(int lecturerId);

        Task<List<GroupMemberResponseDTO>> GetGroupMembersAsync(int lecturerId, int groupId);

        /// <summary>Add multiple students by email or numeric ID in one call.</summary>
        Task<BulkAddResult> AddStudentsToGroupAsync(int lecturerId, int groupId, List<string> studentIdentifiers);

        /// <summary>Remove a student by email or numeric ID.</summary>
        Task RemoveStudentFromGroupAsync(int lecturerId, int groupId, string studentIdentifier);

        Task<StudentGroupResponseDTO> UpdateGroupAsync(int lecturerId, int groupId, UpdateStudentGroupDTO dto);

        Task<List<RequirementResponseDTO>> GetGroupRequirementsAsync(int lecturerId, int groupId);

        Task<List<TaskResponseDTO>> GetGroupTasksAsync(int lecturerId, int groupId);

        Task<List<ProgressReportResponseDTO>> GetProjectProgressReportsAsync(int lecturerId, int groupId);

        Task<GroupCommitStatisticsResponseDTO> GetGithubCommitStatisticsAsync(int lecturerId, int groupId);
    }
}


