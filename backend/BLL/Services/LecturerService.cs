using BLL.DTOs.Admin;
using BLL.Services.Interface;
using DAL.Models;
using DAL.Repositories.Interface;

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

            var groups = await _groupRepository.GetGroupsByLecturerAsync(lecturerId);
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

        public async Task AddStudentToGroupAsync(int lecturerId, int groupId, int studentId)
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

        public async Task RemoveStudentFromGroupAsync(int lecturerId, int groupId, int studentId)
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

            if (!await _memberRepository.IsMemberOfGroupAsync(groupId, studentId))
            {
                throw new Exception("Student is not a member of this group");
            }

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
                MemberCount = group.GroupMembers.Count,
                CreatedAt = group.CreatedAt,
                UpdatedAt = group.UpdatedAt
            };
        }
    }
}
