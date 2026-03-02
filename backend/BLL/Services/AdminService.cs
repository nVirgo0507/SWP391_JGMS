﻿using BLL.DTOs.Admin;
using BLL.Helpers;
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
        private readonly IProjectRepository _projectRepository;
        private readonly PasswordHasher<User> _passwordHasher;

        public AdminService(
            IUserRepository userRepository,
            IStudentGroupRepository groupRepository,
            IGroupMemberRepository memberRepository,
            IProjectRepository projectRepository)
        {
            _userRepository = userRepository;
            _groupRepository = groupRepository;
            _memberRepository = memberRepository;
            _projectRepository = projectRepository;
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

            // Normalize and validate phone number
            var normalizedPhone = PhoneHelper.NormalizePhone(dto.Phone);
            if (!PhoneHelper.IsValidVietnamesePhone(normalizedPhone))
            {
                throw new Exception("Invalid Vietnamese phone number format. Expected: 0XXXXXXXXX (10 digits)");
            }

            // Check phone uniqueness
            if (await _userRepository.PhoneExistsAsync(normalizedPhone))
            {
                throw new Exception("Phone number already exists in the system");
            }

            // Validate student code uniqueness (if provided)
            // Note: Required field validation is handled by CreateUserDTO.Validate()
            if (!string.IsNullOrWhiteSpace(dto.StudentCode) &&
                await _userRepository.StudentCodeExistsAsync(dto.StudentCode))
            {
                throw new Exception("Student code already exists in the system");
            }

            // Validate GitHub username uniqueness (if provided)
            if (!string.IsNullOrWhiteSpace(dto.GithubUsername) &&
                await _userRepository.GithubUsernameExistsAsync(dto.GithubUsername))
            {
                throw new Exception("GitHub username already exists in the system");
            }

            // Validate Jira account ID uniqueness (if provided)
            if (!string.IsNullOrWhiteSpace(dto.JiraAccountId) &&
                await _userRepository.JiraAccountIdExistsAsync(dto.JiraAccountId))
            {
                throw new Exception("Jira account ID already exists in the system");
            }

            var user = new User
            {
                Email = dto.Email,
                FullName = dto.FullName,
                Role = dto.Role,
                StudentCode = dto.StudentCode,
                GithubUsername = dto.GithubUsername,
                JiraAccountId = dto.JiraAccountId,
                Phone = normalizedPhone, // Use normalized phone (converts +84 to 0)
                Status = dto.Status, // BR-006: Active Status Default
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, dto.Password);

            try
            {
                await _userRepository.AddAsync(user);
            }
            catch (Exception ex)
            {
                // Handle database unique constraint violations (race condition protection)
                DatabaseExceptionHandler.HandleUniqueConstraintViolation(ex);
                throw; // Re-throw if not handled
            }

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
            {
                // Check uniqueness if being changed
                if (dto.GithubUsername != user.GithubUsername &&
                    await _userRepository.GithubUsernameExistsAsync(dto.GithubUsername))
                {
                    throw new Exception("GitHub username already exists in the system");
                }
                user.GithubUsername = dto.GithubUsername;
            }

            if (!string.IsNullOrEmpty(dto.JiraAccountId))
            {
                // Check uniqueness if being changed
                if (dto.JiraAccountId != user.JiraAccountId &&
                    await _userRepository.JiraAccountIdExistsAsync(dto.JiraAccountId))
                {
                    throw new Exception("Jira account ID already exists in the system");
                }
                user.JiraAccountId = dto.JiraAccountId;
            }

            // Normalize and validate phone if being changed
            if (!string.IsNullOrEmpty(dto.Phone))
            {
                var normalizedPhone = PhoneHelper.NormalizePhone(dto.Phone);

                if (!PhoneHelper.IsValidVietnamesePhone(normalizedPhone))
                {
                    throw new Exception("Invalid Vietnamese phone number format. Expected: 0XXXXXXXXX (10 digits)");
                }

                // Check phone uniqueness if being changed
                if (normalizedPhone != user.Phone && await _userRepository.PhoneExistsAsync(normalizedPhone))
                {
                    throw new Exception("Phone number already exists in the system");
                }

                user.Phone = normalizedPhone;
            }

            if (dto.Status.HasValue)
                user.Status = dto.Status.Value;

            // Validate role-specific requirements after all updates
            var finalRole = dto.Role ?? user.Role;

            // Phone is required for all roles (check the actual user.Phone which has normalized value)
            if (string.IsNullOrWhiteSpace(user.Phone))
            {
                throw new Exception("Phone number is required for all users");
            }

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
            try
            {
                await _userRepository.UpdateAsync(user);
            }
            catch (Exception ex)
            {
                // Handle database unique constraint violations (race condition protection)
                DatabaseExceptionHandler.HandleUniqueConstraintViolation(ex);
                throw; // Re-throw if not handled
            }

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
                if (await _memberRepository.IsStudentInAnyGroupAsync(dto.LeaderId.Value))
                {
                    throw new Exception($"Student '{leader.FullName}' is already a member of another group and cannot be assigned as leader");
                }
            }

            // Validate initial members if provided
            var validatedMemberIds = new HashSet<int>();
            if (dto.MemberIds != null && dto.MemberIds.Count > 0)
            {
                foreach (var memberId in dto.MemberIds)
                {
                    var member = await _userRepository.GetByIdAsync(memberId);
                    if (member == null)
                    {
                        throw new Exception($"User with ID {memberId} not found");
                    }
                    if (member.Role != UserRole.student)
                    {
                        throw new Exception($"User '{member.FullName}' (ID {memberId}) is not a student (current role: {member.Role})");
                    }
                    // Skip the leader — they'll be added separately and already checked above
                    if (dto.LeaderId.HasValue && memberId == dto.LeaderId.Value)
                    {
                        validatedMemberIds.Add(memberId);
                        continue;
                    }
                    if (await _memberRepository.IsStudentInAnyGroupAsync(memberId))
                    {
                        throw new Exception($"Student '{member.FullName}' is already a member of another group");
                    }
                    validatedMemberIds.Add(memberId);
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

            // Auto-add leader as a group member with is_leader = true
            if (dto.LeaderId.HasValue)
            {
                validatedMemberIds.Add(dto.LeaderId.Value); // ensure leader is in the set
            }

            // Add all members (leader + any extra members from MemberIds)
            foreach (var memberId in validatedMemberIds)
            {
                var groupMember = new GroupMember
                {
                    GroupId = group.GroupId,
                    UserId = memberId,
                    IsLeader = dto.LeaderId.HasValue && memberId == dto.LeaderId.Value,
                    JoinedAt = DateTime.UtcNow
                };
                await _memberRepository.AddAsync(groupMember);
            }

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
                    throw new Exception("Invalid leader ID or user is not a student");

                // New leader must already be an active member of this group
                if (!await _memberRepository.IsMemberOfGroupAsync(groupId, dto.LeaderId.Value))
                    throw new Exception($"Student '{leader.FullName}' is not a member of this group. Add them to the group first before assigning as leader.");

                var oldLeaderId = group.LeaderId;
                group.LeaderId = dto.LeaderId.Value;

                // Clear is_leader on old leader
                if (oldLeaderId.HasValue && oldLeaderId.Value != dto.LeaderId.Value)
                {
                    var oldLeaderMember = await _memberRepository.GetByGroupAndUserIdAsync(groupId, oldLeaderId.Value);
                    if (oldLeaderMember != null)
                    {
                        oldLeaderMember.IsLeader = false;
                        await _memberRepository.UpdateAsync(oldLeaderMember);
                    }
                }

                // Set is_leader = true on the new leader's membership row
                var newLeaderMember = await _memberRepository.GetByGroupAndUserIdAsync(groupId, dto.LeaderId.Value);
                if (newLeaderMember != null && newLeaderMember.IsLeader != true)
                {
                    newLeaderMember.IsLeader = true;
                    await _memberRepository.UpdateAsync(newLeaderMember);
                }
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

            // Remove all group members before soft-deleting
            await _memberRepository.RemoveAllMembersAsync(groupId);

            // Clear the leader reference
            group.LeaderId = null;
            await _groupRepository.UpdateAsync(group);

            // Soft delete - sets status to inactive
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
                throw new Exception("Student group not found");

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
                previous.IsLeader = group.LeaderId == studentId;
                await _memberRepository.RejoinAsync(previous);
            }
            else
            {
                await _memberRepository.AddAsync(new GroupMember
                {
                    GroupId = groupId,
                    UserId = studentId,
                    IsLeader = group.LeaderId == studentId,
                    JoinedAt = DateTime.UtcNow
                });
            }
        }

        public async System.Threading.Tasks.Task RemoveStudentFromGroupAsync(int groupId, int studentId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
                throw new Exception("Student group not found");

            if (!await _memberRepository.IsMemberOfGroupAsync(groupId, studentId))
                throw new Exception("Student is not a member of this group");

            if (group.LeaderId == studentId)
                throw new Exception("Cannot remove the group leader. Assign a different leader first before removing this student.");

            await _memberRepository.RemoveAsync(groupId, studentId);
        }

        public async System.Threading.Tasks.Task ClearGroupMembersOnCompletionAsync(int groupId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
                throw new Exception("Student group not found");

            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project == null)
                throw new Exception("This group has no associated project");

            if (project.Status != ProjectStatus.completed)
                throw new Exception("Cannot clear members: the group's project is not marked as completed");

            // Remove all group members
            await _memberRepository.RemoveAllMembersAsync(groupId);

            // Clear the leader reference on the group
            group.LeaderId = null;
            group.UpdatedAt = DateTime.UtcNow;
            await _groupRepository.UpdateAsync(group);
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
                MemberCount = group.GroupMembers.Count(m => m.LeftAt == null),
                CreatedAt = group.CreatedAt,
                UpdatedAt = group.UpdatedAt
            };
        }

        #endregion

        #region Project Management

        public async Task<ProjectResponseDTO> CreateProjectAsync(int groupId, CreateProjectDTO dto)
        {
            // Validate group exists
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
                throw new Exception($"Group with ID {groupId} not found");

            // Validate group doesn't already have a project (1:1)
            var existing = await _projectRepository.GetByGroupIdAsync(groupId);
            if (existing != null)
                throw new Exception($"Group '{group.GroupCode}' already has a project: '{existing.ProjectName}'");

            // Validate dates
            if (dto.StartDate.HasValue && dto.EndDate.HasValue && dto.EndDate < dto.StartDate)
                throw new Exception("End date cannot be before start date");

            var project = new Project
            {
                GroupId = groupId,
                ProjectName = dto.ProjectName,
                Description = dto.Description,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                Status = ProjectStatus.active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _projectRepository.AddAsync(project);

            // Reload with group details
            var created = await _projectRepository.GetByIdAsync(project.ProjectId);
            return MapToProjectResponse(created!);
        }

        public async Task<ProjectResponseDTO> UpdateProjectAsync(int projectId, UpdateProjectDTO dto)
        {
            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project == null)
                throw new Exception("Project not found");

            if (!string.IsNullOrEmpty(dto.ProjectName))
                project.ProjectName = dto.ProjectName;

            if (dto.Description != null)
                project.Description = dto.Description;

            if (dto.StartDate.HasValue)
                project.StartDate = dto.StartDate;

            if (dto.EndDate.HasValue)
                project.EndDate = dto.EndDate;

            // Validate dates
            if (project.StartDate.HasValue && project.EndDate.HasValue && project.EndDate < project.StartDate)
                throw new Exception("End date cannot be before start date");

            if (!string.IsNullOrEmpty(dto.Status))
            {
                if (Enum.TryParse<ProjectStatus>(dto.Status.ToLower(), out var status))
                    project.Status = status;
                else
                    throw new Exception($"Invalid project status: '{dto.Status}'. Use 'active' or 'completed'.");
            }

            await _projectRepository.UpdateAsync(project);

            var updated = await _projectRepository.GetByIdAsync(project.ProjectId);
            return MapToProjectResponse(updated!);
        }

        public async Task<ProjectResponseDTO?> GetProjectByIdAsync(int projectId)
        {
            var project = await _projectRepository.GetByIdAsync(projectId);
            return project == null ? null : MapToProjectResponse(project);
        }

        public async Task<ProjectResponseDTO?> GetProjectByGroupIdAsync(int groupId)
        {
            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            return project == null ? null : MapToProjectResponse(project);
        }

        public async Task<List<ProjectResponseDTO>> GetAllProjectsAsync()
        {
            var projects = await _projectRepository.GetAllAsync();
            return projects.Select(MapToProjectResponse).ToList();
        }

        public async System.Threading.Tasks.Task DeleteProjectAsync(int projectId)
        {
            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project == null)
                throw new Exception("Project not found");

            var (canDelete, reason) = await _projectRepository.CanDeleteProjectAsync(projectId);
            if (!canDelete)
                throw new Exception(reason);

            await _projectRepository.DeleteAsync(projectId);
        }

        private ProjectResponseDTO MapToProjectResponse(Project project)
        {
            return new ProjectResponseDTO
            {
                ProjectId = project.ProjectId,
                GroupId = project.GroupId,
                GroupCode = project.Group?.GroupCode,
                GroupName = project.Group?.GroupName,
                ProjectName = project.ProjectName,
                Description = project.Description,
                StartDate = project.StartDate,
                EndDate = project.EndDate,
                Status = project.Status?.ToString(),
                CreatedAt = project.CreatedAt,
                UpdatedAt = project.UpdatedAt
            };
        }

        #endregion
    }
}
