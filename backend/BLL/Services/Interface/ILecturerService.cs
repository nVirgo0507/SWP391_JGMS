using BLL.DTOs.Admin;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    /// <summary>
    /// Lecturer Service Interface
    /// BR-054: Lecturer Group-Scoped Access - Lecturers can only access groups assigned to them
    /// All methods validate lecturer_id matches the group assignment
    /// Error Message: "Access denied. You are not assigned to this group."
    /// </summary>
    public interface ILecturerService
    {
        /// <summary>
        /// BR-054: Get group details - Only accessible by assigned lecturer
        /// Validates lecturer is assigned to the group
        /// </summary>
        Task<StudentGroupResponseDTO?> GetGroupByIdAsync(int lecturerId, int groupId);

        /// <summary>
        /// BR-054: Get all groups assigned to the lecturer
        /// Only returns groups where lecturer_id matches current user
        /// </summary>
        Task<List<StudentGroupResponseDTO>> GetMyGroupsAsync(int lecturerId);

        /// <summary>
        /// BR-054: Get all members in a group assigned to the lecturer
        /// Validates lecturer access before retrieving members
        /// </summary>
        Task<List<GroupMemberResponseDTO>> GetGroupMembersAsync(int lecturerId, int groupId);

        /// <summary>
        /// BR-054: Add student to group assigned to the lecturer
        /// Validates lecturer access and student is not already a member
        /// </summary>
        Task AddStudentToGroupAsync(int lecturerId, int groupId, int studentId);

        /// <summary>
        /// BR-054: Remove student from group assigned to the lecturer
        /// Validates lecturer access and student is a member
        /// </summary>
        Task RemoveStudentFromGroupAsync(int lecturerId, int groupId, int studentId);

        /// <summary>
        /// BR-054: Update group details - Only assigned lecturer can update
        /// Validates lecturer access before allowing updates
        /// </summary>
        Task<StudentGroupResponseDTO> UpdateGroupAsync(int lecturerId, int groupId, UpdateStudentGroupDTO dto);
    }
}
