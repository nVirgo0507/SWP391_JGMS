﻿﻿using BLL.DTOs.Admin;
using BLL.Services.Interface;
using DAL.Models;
using DAL.Repositories.Interface;
using Microsoft.AspNetCore.Identity;
using System.Text.RegularExpressions;

namespace BLL.Services
{
    public class AdminService : IAdminService
    {
        private readonly IUserRepository _userRepository;
        private readonly IStudentGroupRepository _groupRepository;
        private readonly IGroupMemberRepository _memberRepository;
        private readonly PasswordHasher<User> _passwordHasher;

        public AdminService(
            IUserRepository userRepository,
            IStudentGroupRepository groupRepository,
            IGroupMemberRepository memberRepository)
        {
            _userRepository = userRepository;
            _groupRepository = groupRepository;
            _memberRepository = memberRepository;
            _passwordHasher = new PasswordHasher<User>();
        }

        #region User Management

        public async Task<UserResponseDTO> CreateUserAsync(CreateUserDTO dto)
        {
            // BR-001: Unique Email Address
            if (await _userRepository.EmailExistsAsync(dto.Email))
            {
                throw new Exception("Email address already exists in the system");
            }

            // BR-002: Role Assignment - Each user must be assigned exactly one role
            if (!Enum.IsDefined(typeof(UserRole), dto.Role))
            {
                throw new Exception("Invalid role selected");
            }

            // BR-005: Password Strength
            var regex = new Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$");
            if (!regex.IsMatch(dto.Password))
            {
                throw new Exception("Password must be at least 8 characters with uppercase, lowercase, and number");
            }

            // Role-specific validation
            if (dto.Role == UserRole.student)
            {
                // Students must have student code, github username, and jira account id
                if (string.IsNullOrWhiteSpace(dto.StudentCode))
                {
                    throw new Exception("Student code is required for students");
                }

                if (string.IsNullOrWhiteSpace(dto.GithubUsername))
                {
                    throw new Exception("GitHub username is required for students");
                }

                if (string.IsNullOrWhiteSpace(dto.JiraAccountId))
                {
                    throw new Exception("Jira account ID is required for students");
                }

                // Validate student code uniqueness
                if (await _userRepository.StudentCodeExistsAsync(dto.StudentCode))
                {
                    throw new Exception("Student code already exists in the system");
                }
            }

            var user = new User
            {
                Email = dto.Email,
                FullName = dto.FullName,
                Role = dto.Role,
                StudentCode = dto.StudentCode,
                GithubUsername = dto.GithubUsername,
                JiraAccountId = dto.JiraAccountId,
                Phone = dto.Phone,
                Status = dto.Status, // BR-006: Active Status Default
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, dto.Password);
            await _userRepository.AddAsync(user);

            return MapToUserResponse(user);
        }

        public async Task<UserResponseDTO> UpdateUserAsync(int userId, UpdateUserDTO dto)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new Exception("User not found");
            }

            // BR-001: Unique Email Address (if email is being changed)
            if (!string.IsNullOrEmpty(dto.Email) && dto.Email != user.Email)
            {
                if (await _userRepository.EmailExistsAsync(dto.Email))
                {
                    throw new Exception("Email address already exists in the system");
                }
                user.Email = dto.Email;
            }

            // Validate student code uniqueness if being changed
            if (!string.IsNullOrEmpty(dto.StudentCode) && dto.StudentCode != user.StudentCode)
            {
                if (await _userRepository.StudentCodeExistsAsync(dto.StudentCode))
                {
                    throw new Exception("Student code already exists in the system");
                }
                user.StudentCode = dto.StudentCode;
            }

            // Update other fields if provided
            if (!string.IsNullOrEmpty(dto.FullName))
                user.FullName = dto.FullName;

            // BR-002: Role Assignment - Each user must be assigned exactly one role
            if (dto.Role.HasValue)
            {
                if (!Enum.IsDefined(typeof(UserRole), dto.Role.Value))
                {
                    throw new Exception("Invalid role selected");
                }
                user.Role = dto.Role.Value;
            }

            if (!string.IsNullOrEmpty(dto.GithubUsername))
                user.GithubUsername = dto.GithubUsername;

            if (!string.IsNullOrEmpty(dto.JiraAccountId))
                user.JiraAccountId = dto.JiraAccountId;

            if (!string.IsNullOrEmpty(dto.Phone))
                user.Phone = dto.Phone;

            if (dto.Status.HasValue)
                user.Status = dto.Status.Value;

            // Validate role-specific requirements after all updates
            var finalRole = dto.Role ?? user.Role;
            if (finalRole == UserRole.student)
            {
                // Ensure student has all required fields
                var finalStudentCode = dto.StudentCode ?? user.StudentCode;
                var finalGithubUsername = dto.GithubUsername ?? user.GithubUsername;
                var finalJiraAccountId = dto.JiraAccountId ?? user.JiraAccountId;

                if (string.IsNullOrWhiteSpace(finalStudentCode))
                {
                    throw new Exception("Student code is required for students");
                }

                if (string.IsNullOrWhiteSpace(finalGithubUsername))
                {
                    throw new Exception("GitHub username is required for students");
                }

                if (string.IsNullOrWhiteSpace(finalJiraAccountId))
                {
                    throw new Exception("Jira account ID is required for students");
                }
            }
            // BR-060: Preserve Audit Trail (UpdatedAt is handled in repository)
            await _userRepository.UpdateAsync(user);

            return MapToUserResponse(user);
        }

        public async Task<UserResponseDTO?> GetUserByIdAsync(int userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            return user != null ? MapToUserResponse(user) : null;
        }

        public async Task<List<UserResponseDTO>> GetAllUsersAsync()
        {
            var users = await _userRepository.GetAllAsync();
            return users.Select(MapToUserResponse).ToList();
        }

        public async Task<List<UserResponseDTO>> GetUsersByRoleAsync(string role)
        {
            // BR-002: Role Assignment - Validate role parameter
            if (!Enum.TryParse<UserRole>(role, true, out var userRole))
            {
                throw new Exception("Invalid role selected");
            }

            var users = await _userRepository.GetByRoleAsync(userRole);
            return users.Select(MapToUserResponse).ToList();
        }

        public async System.Threading.Tasks.Task DeleteUserAsync(int userId)
        {
            // Verify user exists
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new Exception("User not found");
            }

            // BR-059: Cascade Delete Prevention
            if (!await _userRepository.CanDeleteUserAsync(userId))
            {
                throw new Exception("Cannot delete user: user has associated data (groups, tasks, requirements). Please remove or reassign these first.");
            }

            await _userRepository.DeleteAsync(userId);
        }

        public async System.Threading.Tasks.Task SetUserStatusAsync(int userId, string status)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new Exception("User not found");
            }

            if (!Enum.TryParse<UserStatus>(status, true, out var userStatus))
            {
                throw new Exception("Invalid status specified");
            }

            // BR-007: Inactive Users Cannot Login
            user.Status = userStatus;
            await _userRepository.UpdateAsync(user);
        }

        #endregion

        #region Student Group Management

        public async Task<StudentGroupResponseDTO> CreateStudentGroupAsync(CreateStudentGroupDTO dto)
        {
            // Validate group code uniqueness
            if (await _groupRepository.GroupCodeExistsAsync(dto.GroupCode))
            {
                throw new Exception("Group code already exists in the system");
            }

            // Validate lecturer exists and has correct role
            var lecturer = await _userRepository.GetByIdAsync(dto.LecturerId);
            if (lecturer == null)
            {
                throw new Exception($"Lecturer with ID {dto.LecturerId} not found");
            }
            if (lecturer.Role != UserRole.lecturer)
            {
                throw new Exception($"User with ID {dto.LecturerId} is not a lecturer (current role: {lecturer.Role})");
            }

            // Validate leader if provided
            if (dto.LeaderId.HasValue)
            {
                var leader = await _userRepository.GetByIdAsync(dto.LeaderId.Value);
                if (leader == null)
                {
                    throw new Exception($"Leader with ID {dto.LeaderId.Value} not found");
                }
                if (leader.Role != UserRole.student)
                {
                    throw new Exception($"User with ID {dto.LeaderId.Value} is not a student (current role: {leader.Role})");
                }
            }

            var group = new StudentGroup
            {
                GroupCode = dto.GroupCode,
                GroupName = dto.GroupName,
                LecturerId = dto.LecturerId,
                LeaderId = dto.LeaderId,
                Status = UserStatus.active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _groupRepository.AddAsync(group);

            // Reload with details
            var createdGroup = await _groupRepository.GetGroupWithDetailsAsync(group.GroupId);
            return MapToGroupResponse(createdGroup!);
        }

        public async Task<StudentGroupResponseDTO> UpdateStudentGroupAsync(int groupId, UpdateStudentGroupDTO dto)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
            {
                throw new Exception("Student group not found");
            }

            // Validate group code uniqueness if being changed
            if (!string.IsNullOrEmpty(dto.GroupCode) && dto.GroupCode != group.GroupCode)
            {
                if (await _groupRepository.GroupCodeExistsAsync(dto.GroupCode))
                {
                    throw new Exception("Group code already exists in the system");
                }
                group.GroupCode = dto.GroupCode;
            }

            // Update fields if provided
            if (!string.IsNullOrEmpty(dto.GroupName))
                group.GroupName = dto.GroupName;

            if (dto.LecturerId.HasValue)
            {
                var lecturer = await _userRepository.GetByIdAsync(dto.LecturerId.Value);
                if (lecturer == null || lecturer.Role != UserRole.lecturer)
                {
                    throw new Exception("Invalid lecturer ID or user is not a lecturer");
                }
                group.LecturerId = dto.LecturerId.Value;
            }

            if (dto.LeaderId.HasValue)
            {
                var leader = await _userRepository.GetByIdAsync(dto.LeaderId.Value);
                if (leader == null || leader.Role != UserRole.student)
                {
                    throw new Exception("Invalid leader ID or user is not a student");
                }
                group.LeaderId = dto.LeaderId.Value;
            }

            if (dto.Status.HasValue)
                group.Status = dto.Status.Value;

            await _groupRepository.UpdateAsync(group);

            // Reload with details
            var updatedGroup = await _groupRepository.GetGroupWithDetailsAsync(groupId);
            return MapToGroupResponse(updatedGroup!);
        }

        public async Task<StudentGroupResponseDTO?> GetStudentGroupByIdAsync(int groupId)
        {
            var group = await _groupRepository.GetGroupWithDetailsAsync(groupId);
            return group != null ? MapToGroupResponse(group) : null;
        }

        public async Task<List<StudentGroupResponseDTO>> GetAllStudentGroupsAsync()
        {
            var groups = await _groupRepository.GetAllAsync();
            return groups.Select(MapToGroupResponse).ToList();
        }

        public async System.Threading.Tasks.Task DeleteStudentGroupAsync(int groupId)
        {
            // BR-061: Soft Delete for Groups
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
            {
                throw new Exception("Student group not found");
            }

            // Check if group can be deleted (no associated project)
            if (!await _groupRepository.CanDeleteGroupAsync(groupId))
            {
                throw new Exception("Cannot delete group: group has an associated project. Please delete or reassign the project first.");
            }

            // Use soft delete
            await _groupRepository.DeleteAsync(groupId);
        }

        #endregion

        #region Lecturer Management

        public async Task<List<UserResponseDTO>> GetAllLecturersAsync()
        {
            var lecturers = await _userRepository.GetByRoleAsync(UserRole.lecturer);
            return lecturers.Select(MapToUserResponse).ToList();
        }

        public async System.Threading.Tasks.Task AssignLecturerToGroupAsync(int groupId, int lecturerId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
            {
                throw new Exception("Student group not found");
            }

            var lecturer = await _userRepository.GetByIdAsync(lecturerId);
            if (lecturer == null || lecturer.Role != UserRole.lecturer)
            {
                throw new Exception("Invalid lecturer ID or user is not a lecturer");
            }

            // BR-007: Inactive Users Cannot Login - also shouldn't be assigned
            if (lecturer.Status == UserStatus.inactive)
            {
                throw new Exception("Cannot assign inactive lecturer to group");
            }

            group.LecturerId = lecturerId;
            await _groupRepository.UpdateAsync(group);
        }

        #endregion

        #region Group Member Management

        public async System.Threading.Tasks.Task AddStudentToGroupAsync(int groupId, int studentId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
            {
                throw new Exception("Student group not found");
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
                IsLeader = group.LeaderId == studentId,
                JoinedAt = DateTime.UtcNow
            };

            await _memberRepository.AddAsync(member);
        }

        public async System.Threading.Tasks.Task RemoveStudentFromGroupAsync(int groupId, int studentId)
        {
            if (!await _memberRepository.IsMemberOfGroupAsync(groupId, studentId))
            {
                throw new Exception("Student is not a member of this group");
            }

            await _memberRepository.RemoveAsync(groupId, studentId);
        }

        #endregion

        #region Helper Methods

        private UserResponseDTO MapToUserResponse(User user)
        {
            return new UserResponseDTO
            {
                UserId = user.UserId,
                Email = user.Email,
                FullName = user.FullName,
                Role = user.Role,
                StudentCode = user.StudentCode,
                GithubUsername = user.GithubUsername,
                JiraAccountId = user.JiraAccountId,
                Phone = user.Phone,
                Status = user.Status,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };
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

        #endregion
    }
}
