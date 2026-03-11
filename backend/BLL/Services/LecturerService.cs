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

        public async Task<BulkAddResult> AddStudentsToGroupAsync(int lecturerId, int groupId, List<string> studentIdentifiers)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
                throw new Exception("Student group not found");

            if (group.LecturerId != lecturerId)
                throw new Exception("Access denied. You are not assigned to this group.");

            var result = new BulkAddResult();

            foreach (var identifier in studentIdentifiers)
            {
                try
                {
                    // Resolve identifier → student ID
                    User? student = null;
                    if (int.TryParse(identifier, out var numId))
                        student = await _userRepository.GetByIdAsync(numId);
                    else
                        student = await _userRepository.GetByEmailAsync(identifier);

                    if (student == null)
                        throw new Exception($"User '{identifier}' not found.");
                    if (student.Role != UserRole.student)
                        throw new Exception($"'{identifier}' is not a student account.");

                    if (await _memberRepository.IsMemberOfGroupAsync(groupId, student.UserId))
                        throw new Exception("Already a member of this group.");

                    if (await _memberRepository.IsStudentInAnyGroupAsync(student.UserId))
                        throw new Exception("Already a member of another group.");

                    var previous = await _memberRepository.GetPreviousMembershipAsync(groupId, student.UserId);
                    if (previous != null)
                        await _memberRepository.RejoinAsync(previous);
                    else
                        await _memberRepository.AddAsync(new GroupMember
                        {
                            GroupId = groupId,
                            UserId = student.UserId,
                            IsLeader = false,
                            JoinedAt = DateTime.UtcNow
                        });

                    result.Added.Add(identifier);
                    result.SuccessCount++;
                }
                catch (Exception ex)
                {
                    result.Failures.Add(new BulkAddFailure { Identifier = identifier, Reason = ex.Message });
                    result.FailureCount++;
                }
            }

            return result;
        }

        public async System.Threading.Tasks.Task RemoveStudentFromGroupAsync(int lecturerId, int groupId, string studentIdentifier)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
                throw new Exception("Student group not found");

            if (group.LecturerId != lecturerId)
                throw new Exception("Access denied. You are not assigned to this group.");

            User? student = null;
            if (int.TryParse(studentIdentifier, out var numId))
                student = await _userRepository.GetByIdAsync(numId);
            else
                student = await _userRepository.GetByEmailAsync(studentIdentifier);

            if (student == null)
                throw new KeyNotFoundException($"Student '{studentIdentifier}' not found.");

            if (!await _memberRepository.IsMemberOfGroupAsync(groupId, student.UserId))
                throw new Exception("Student is not a member of this group");

            if (group.LeaderId == student.UserId)
                throw new Exception("Cannot remove the group leader. Assign a different leader first before removing this student.");

            await _memberRepository.RemoveAsync(groupId, student.UserId);
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
            var activeMembers = group.GroupMembers
                .Where(m => m.LeftAt == null)
                .OrderByDescending(m => m.IsLeader)
                .ThenBy(m => m.User.FullName)
                .Select(m => new GroupMemberResponseDTO
                {
                    MemberId = m.MembershipId,
                    GroupId  = m.GroupId,
                    UserId   = m.UserId,
                    UserName = m.User.FullName,
                    Email    = m.User.Email,
                    IsLeader = m.IsLeader.GetValueOrDefault(false),
                    JoinedAt = m.JoinedAt,
                    LeftAt   = m.LeftAt
                })
                .ToList();

            return new StudentGroupResponseDTO
            {
                GroupId      = group.GroupId,
                GroupCode    = group.GroupCode,
                GroupName    = group.GroupName,
                LecturerId   = group.LecturerId,
                LecturerName = group.Lecturer.FullName,
                LeaderId     = group.LeaderId,
                LeaderName   = group.Leader?.FullName,
                Status       = group.Status,
                MemberCount  = activeMembers.Count,
                Members      = activeMembers,
                CreatedAt    = group.CreatedAt,
                UpdatedAt    = group.UpdatedAt
            };
        }
    }
}
