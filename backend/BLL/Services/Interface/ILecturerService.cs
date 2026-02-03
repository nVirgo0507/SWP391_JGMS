using BLL.DTOs.Admin;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface ILecturerService
    {
        Task<StudentGroupResponseDTO?> GetGroupByIdAsync(int lecturerId, int groupId);

        Task<List<StudentGroupResponseDTO>> GetMyGroupsAsync(int lecturerId);

        Task<List<GroupMemberResponseDTO>> GetGroupMembersAsync(int lecturerId, int groupId);

        Task AddStudentToGroupAsync(int lecturerId, int groupId, int studentId);

        Task RemoveStudentFromGroupAsync(int lecturerId, int groupId, int studentId);

        Task<StudentGroupResponseDTO> UpdateGroupAsync(int lecturerId, int groupId, UpdateStudentGroupDTO dto);
    }
}
