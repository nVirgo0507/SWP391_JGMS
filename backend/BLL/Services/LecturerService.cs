using BLL.DTOs.Admin;
using BLL.Services.Interface;
using DAL.Models;
using DAL.Repositories.Interface;

namespace BLL.Services
{
    /// <summary>
    /// Service for lecturer-scoped operations with group access control
    /// BR-054: Lecturer Group-Scoped Access - Lecturers can only access groups assigned to them
    /// </summary>
    public class LecturerService : ILecturerService
    {
        private readonly IStudentGroupRepository _groupRepository;
        private readonly IUserRepository _userRepository;
        private readonly IGroupMemberRepository _memberRepository;

        public LecturerService(
            IStudentGroupRepository groupRepository,
            IUserRepository userRepository,
            IGroupMemberRepository memberRepository)
        {
            _groupRepository = groupRepository;
            _userRepository = userRepository;
            _memberRepository = memberRepository;
        }

        /// <summary>
        /// BR-054: Get group details - Only accessible by assigned lecturer
        /// Validates that the lecturer is assigned to the group before returning details
        /// </summary>
        public async Task<StudentGroupResponseDTO?> GetGroupByIdAsync(int lecturerId, int groupId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
            {
                throw new Exception("Student group not found");
            }

            // BR-054: Lecturer Group-Scoped Access - Check lecturer_id matches
            if (group.LecturerId != lecturerId)
            {
                throw new Exception("Access denied. You are not assigned to this group.");
            }

            var groupDetails = await _groupRepository.GetGroupWithDetailsAsync(groupId);
            return groupDetails != null ? MapToGroupResponse(groupDetails) : null;
        }

        /// <summary>
        /// BR-054: Get all groups assigned to the lecturer
        /// Only returns groups where lecturer_id matches the current user
        /// </summary>
        public async Task<List<StudentGroupResponseDTO>> GetMyGroupsAsync(int lecturerId)
        {
            // Verify lecturer exists
            var lecturer = await _userRepository.GetByIdAsync(lecturerId);
            if (lecturer == null || lecturer.Role != UserRole.lecturer)
            {
                throw new Exception("Lecturer not found or invalid user role");
            }

            // BR-054: Get only groups assigned to this lecturer
            var groups = await _groupRepository.GetGroupsByLecturerAsync(lecturerId);
            return groups.Select(MapToGroupResponse).ToList();
        }

        /// <summary>
        /// BR-054: Get all members in a group assigned to the lecturer
        /// Validates lecturer access before retrieving members
        /// </summary>
        public async Task<List<GroupMemberResponseDTO>> GetGroupMembersAsync(int lecturerId, int groupId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
            {
                throw new Exception("Student group not found");
            }

            // BR-054: Lecturer Group-Scoped Access - Check lecturer_id matches
            if (group.LecturerId != lecturerId)
            {
                throw new Exception("Access denied. You are not assigned to this group.");
            }

            var members = await _memberRepository.GetMembersByGroupIdAsync(groupId);
            return members.Select(m => new GroupMemberResponseDTO
            {
                MemberId = m.MembershipId,
                GroupId = m.GroupId,
                UserId = m.UserId,
                UserName = m.User.FullName,
                Email = m.User.Email,
                IsLeader = m.IsLeader,
                JoinedAt = m.JoinedAt
            }).ToList();
        }

        /// <summary>
        /// BR-054: Add student to group assigned to the lecturer
        /// Validates lecturer access before adding member
        /// </summary>
        public async Task AddStudentToGroupAsync(int lecturerId, int groupId, int studentId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
            {
                throw new Exception("Student group not found");
            }

            // BR-054: Lecturer Group-Scoped Access - Check lecturer_id matches
            if (group.LecturerId != lecturerId)
            {
                throw new Exception("Access denied. You are not assigned to this group.");
            }

            var student = await _userRepository.GetByIdAsync(studentId);
            if (student == null || student.Role != UserRole.student)
            {
                throw new Exception("Invalid student ID or user is not a student");
            }

            if (await _memberRepository.IsMemberOfGroupAsync(groupId, studentId))
            {
                throw new Exception("Student is already a member of this group");
            }

            var member = new GroupMember
            {
                GroupId = groupId,
                UserId = studentId,
                IsLeader = false,
                JoinedAt = DateTime.UtcNow
            };

            await _memberRepository.AddAsync(member);
        }

        /// <summary>
        /// BR-054: Remove student from group assigned to the lecturer
        /// Validates lecturer access before removing member
        /// </summary>
        public async Task RemoveStudentFromGroupAsync(int lecturerId, int groupId, int studentId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
            {
                throw new Exception("Student group not found");
            }

            // BR-054: Lecturer Group-Scoped Access - Check lecturer_id matches
            if (group.LecturerId != lecturerId)
            {
                throw new Exception("Access denied. You are not assigned to this group.");
            }

            if (!await _memberRepository.IsMemberOfGroupAsync(groupId, studentId))
            {
                throw new Exception("Student is not a member of this group");
            }

            await _memberRepository.RemoveAsync(groupId, studentId);
        }

        /// <summary>
        /// BR-054: Update group details - Only assigned lecturer can update
        /// Validates lecturer access before allowing updates
        /// </summary>
        public async Task<StudentGroupResponseDTO> UpdateGroupAsync(int lecturerId, int groupId, UpdateStudentGroupDTO dto)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
            {
                throw new Exception("Student group not found");
            }

            // BR-054: Lecturer Group-Scoped Access - Check lecturer_id matches
            if (group.LecturerId != lecturerId)
            {
                throw new Exception("Access denied. You are not assigned to this group.");
            }

            // Update group name if provided
            if (!string.IsNullOrEmpty(dto.GroupName))
                group.GroupName = dto.GroupName;

            // BR-054: Only lecturer can update status for their group
            if (dto.Status.HasValue)
                group.Status = dto.Status.Value;

            group.UpdatedAt = DateTime.UtcNow;
            await _groupRepository.UpdateAsync(group);

            var updatedGroup = await _groupRepository.GetGroupWithDetailsAsync(groupId);
            return MapToGroupResponse(updatedGroup!);
        }

        #region Helper Methods

        private StudentGroupResponseDTO MapToGroupResponse(StudentGroup group)
        {
            return new StudentGroupResponseDTO
            {
                GroupId = group.GroupId,
                GroupCode = group.GroupCode,
                GroupName = group.GroupName,
                LecturerId = group.LecturerId,
                LecturerName = group.Lecturer.FullName,
                LeaderId = group.LeaderId,
                LeaderName = group.Leader?.FullName,
                Status = group.Status,
                MemberCount = group.GroupMembers.Count,
                CreatedAt = group.CreatedAt,
                UpdatedAt = group.UpdatedAt
            };
        }

        #endregion
    }
}
