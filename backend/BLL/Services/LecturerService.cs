using BLL.DTOs.Admin;
using BLL.Services.Interface;
using DAL.Models;
using DAL.Repositories.Interface;
using System.Threading.Tasks;

namespace BLL.Services
{
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

        public async Task<StudentGroupResponseDTO?> GetGroupByIdAsync(int lecturerId, int groupId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
            {
                throw new Exception("Student group not found");
            }

            if (group.LecturerId != lecturerId)
            {
                throw new Exception("Access denied. You are not assigned to this group.");
            }

            var groupDetails = await _groupRepository.GetGroupWithDetailsAsync(groupId);
            return groupDetails != null ? MapToGroupResponse(groupDetails) : null;
        }

        public async Task<List<StudentGroupResponseDTO>> GetMyGroupsAsync(int lecturerId)
        {
            var lecturer = await _userRepository.GetByIdAsync(lecturerId);
            if (lecturer == null || lecturer.Role != UserRole.lecturer)
            {
                throw new Exception("Lecturer not found or invalid user role");
            }

            var groups = await _groupRepository.GetByLecturerIdAsync(lecturerId);
            return groups.Select(MapToGroupResponse).ToList();
        }

        public async Task<List<GroupMemberResponseDTO>> GetGroupMembersAsync(int lecturerId, int groupId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
            {
                throw new Exception("Student group not found");
            }

            if (group.LecturerId != lecturerId)
            {
                throw new Exception("Access denied. You are not assigned to this group.");
            }

            var members = await _memberRepository.GetByGroupIdAsync(groupId);
            return members.Select(m => new GroupMemberResponseDTO
            {
                MemberId = m.MembershipId,
                GroupId = m.GroupId,
                UserId = m.UserId,
                UserName = m.User.FullName,
                Email = m.User.Email,
                IsLeader = m.IsLeader.GetValueOrDefault(false),
                JoinedAt = m.JoinedAt,
                LeftAt = m.LeftAt
            }).ToList();
        }

        public async System.Threading.Tasks.Task AddStudentToGroupAsync(int lecturerId, int groupId, int studentId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
                throw new Exception("Student group not found");

            if (group.LecturerId != lecturerId)
                throw new Exception("Access denied. You are not assigned to this group.");

            var student = await _userRepository.GetByIdAsync(studentId);
            if (student == null || student.Role != UserRole.student)
                throw new Exception("Invalid student ID or user is not a student");

            if (await _memberRepository.IsMemberOfGroupAsync(groupId, studentId))
                throw new Exception("Student is already an active member of this group");

            if (await _memberRepository.IsStudentInAnyGroupAsync(studentId))
                throw new Exception($"Student '{student.FullName}' is already an active member of another group");

            // Re-activate previous membership if one exists, otherwise create a new one
            var previous = await _memberRepository.GetPreviousMembershipAsync(groupId, studentId);
            if (previous != null)
            {
                await _memberRepository.RejoinAsync(previous);
            }
            else
            {
                await _memberRepository.AddAsync(new GroupMember
                {
                    GroupId = groupId,
                    UserId = studentId,
                    IsLeader = false,
                    JoinedAt = DateTime.UtcNow
                });
            }
        }

        public async System.Threading.Tasks.Task RemoveStudentFromGroupAsync(int lecturerId, int groupId, int studentId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
                throw new Exception("Student group not found");

            if (group.LecturerId != lecturerId)
                throw new Exception("Access denied. You are not assigned to this group.");

            if (!await _memberRepository.IsMemberOfGroupAsync(groupId, studentId))
                throw new Exception("Student is not a member of this group");

            if (group.LeaderId == studentId)
                throw new Exception("Cannot remove the group leader. Assign a different leader first before removing this student.");

            await _memberRepository.RemoveAsync(groupId, studentId);
        }

        public async Task<StudentGroupResponseDTO> UpdateGroupAsync(int lecturerId, int groupId, UpdateStudentGroupDTO dto)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
            {
                throw new Exception("Student group not found");
            }

            if (group.LecturerId != lecturerId)
            {
                throw new Exception("Access denied. You are not assigned to this group.");
            }

            if (!string.IsNullOrEmpty(dto.GroupName))
                group.GroupName = dto.GroupName;

            if (dto.Status.HasValue)
                group.Status = dto.Status.Value;

            group.UpdatedAt = DateTime.UtcNow;
            await _groupRepository.UpdateAsync(group);

            var updatedGroup = await _groupRepository.GetGroupWithDetailsAsync(groupId);
            return MapToGroupResponse(updatedGroup!);
        }

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
                MemberCount = group.GroupMembers.Count(m => m.LeftAt == null),
                CreatedAt = group.CreatedAt,
                UpdatedAt = group.UpdatedAt
            };
        }
    }
}
