using BLL.DTOs.Admin;
using BLL.Helpers;
using BLL.Services.Interface;
using DAL.Models;
using DAL.Repositories.Interface;
using Microsoft.AspNetCore.Identity;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using Task = System.Threading.Tasks.Task;

namespace BLL.Services
{
    public class AdminService : IAdminService
    {
        private readonly IUserRepository _userRepository;
        private readonly IStudentGroupRepository _groupRepository;
        private readonly IGroupMemberRepository _memberRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IJiraIntegrationRepository _jiraIntegrationRepository;
        private readonly IGithubIntegrationRepository _githubIntegrationRepository;
        private readonly IJiraIssueRepository _jiraIssueRepository;
        private readonly IGithubCommitRepository _githubCommitRepository;
        private readonly PasswordHasher<User> _passwordHasher;

        public AdminService(
            IUserRepository userRepository,
            IStudentGroupRepository groupRepository,
            IGroupMemberRepository memberRepository,
            IProjectRepository projectRepository,
            IJiraIntegrationRepository jiraIntegrationRepository,
            IGithubIntegrationRepository githubIntegrationRepository,
            IJiraIssueRepository jiraIssueRepository,
            IGithubCommitRepository githubCommitRepository)
        {
            _userRepository = userRepository;
            _groupRepository = groupRepository;
            _memberRepository = memberRepository;
            _projectRepository = projectRepository;
            _jiraIntegrationRepository = jiraIntegrationRepository;
            _githubIntegrationRepository = githubIntegrationRepository;
            _jiraIssueRepository = jiraIssueRepository;
            _githubCommitRepository = githubCommitRepository;
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

        public async Task<List<UserResponseDTO>> SearchUsersAsync(string query, string? role = null)
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new Exception("Search query cannot be empty");

            UserRole? userRole = null;
            if (!string.IsNullOrWhiteSpace(role))
            {
                if (!Enum.TryParse<UserRole>(role, true, out var parsedRole))
                    throw new Exception($"Invalid role '{role}'. Valid roles: admin, lecturer, student");
                userRole = parsedRole;
            }

            var users = await _userRepository.SearchByNameOrEmailAsync(query, userRole);
            return users.Select(MapToUserResponse).ToList();
        }

        public async Task<List<UserResponseDTO>> GetAvailableStudentsAsync()
        {
            var students = await _userRepository.GetAvailableStudentsAsync();
            return students.Select(MapToUserResponse).ToList();
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

            // Resolve and validate lecturer (accepts email or numeric ID)
            int resolvedLecturerId;
            try { resolvedLecturerId = await ResolveUserIdentifierAsync(dto.LecturerId); }
            catch (KeyNotFoundException) { throw new Exception($"Lecturer '{dto.LecturerId}' not found. Provide a valid email or numeric user ID."); }

            var lecturer = await _userRepository.GetByIdAsync(resolvedLecturerId);
            if (lecturer!.Role != UserRole.lecturer)
                throw new Exception($"User '{dto.LecturerId}' is not a lecturer (current role: {lecturer.Role})");

            // Resolve and validate leader if provided (accepts email or numeric ID)
            int? resolvedLeaderId = null;
            if (!string.IsNullOrWhiteSpace(dto.LeaderId))
            {
                try { resolvedLeaderId = await ResolveUserIdentifierAsync(dto.LeaderId); }
                catch (KeyNotFoundException) { throw new Exception($"Leader '{dto.LeaderId}' not found. Provide a valid email or numeric user ID."); }

                var leader = await _userRepository.GetByIdAsync(resolvedLeaderId.Value);
                if (leader!.Role != UserRole.student)
                    throw new Exception($"User '{dto.LeaderId}' is not a student (current role: {leader.Role})");
                if (await _memberRepository.IsStudentInAnyGroupAsync(resolvedLeaderId.Value))
                    throw new Exception($"Student '{leader.FullName}' is already a member of another group and cannot be assigned as leader");
            }

            // Resolve and validate initial members if provided (accepts email or numeric ID)
            var validatedMemberIds = new HashSet<int>();
            if (dto.MemberIds != null && dto.MemberIds.Count > 0)
            {
                foreach (var memberIdentifier in dto.MemberIds)
                {
                    int memberId;
                    try { memberId = await ResolveUserIdentifierAsync(memberIdentifier); }
                    catch (KeyNotFoundException) { throw new Exception($"Member '{memberIdentifier}' not found. Provide a valid email or numeric user ID."); }

                    var member = await _userRepository.GetByIdAsync(memberId);
                    if (member!.Role != UserRole.student)
                        throw new Exception($"User '{memberIdentifier}' ({member.FullName}) is not a student (current role: {member.Role})");

                    // Skip duplicate check for the leader — they'll be added separately
                    if (resolvedLeaderId.HasValue && memberId == resolvedLeaderId.Value)
                    {
                        validatedMemberIds.Add(memberId);
                        continue;
                    }
                    if (await _memberRepository.IsStudentInAnyGroupAsync(memberId))
                        throw new Exception($"Student '{member.FullName}' is already a member of another group");
                    validatedMemberIds.Add(memberId);
                }
            }

            var group = new StudentGroup
            {
                GroupCode = dto.GroupCode,
                GroupName = dto.GroupName,
                LecturerId = resolvedLecturerId,
                LeaderId = resolvedLeaderId,
                Status = UserStatus.active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _groupRepository.AddAsync(group);

            // Auto-add leader as a group member with is_leader = true
            if (resolvedLeaderId.HasValue)
                validatedMemberIds.Add(resolvedLeaderId.Value);

            // Add all members (leader + any extra members from MemberIds)
            foreach (var memberId in validatedMemberIds)
            {
                var groupMember = new GroupMember
                {
                    GroupId = group.GroupId,
                    UserId = memberId,
                    IsLeader = resolvedLeaderId.HasValue && memberId == resolvedLeaderId.Value,
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

            if (!string.IsNullOrWhiteSpace(dto.LecturerId))
            {
                int resolvedLecturerId;
                try { resolvedLecturerId = await ResolveUserIdentifierAsync(dto.LecturerId); }
                catch (KeyNotFoundException) { throw new Exception($"Lecturer '{dto.LecturerId}' not found. Provide a valid email or numeric user ID."); }

                var lecturer = await _userRepository.GetByIdAsync(resolvedLecturerId);
                if (lecturer == null || lecturer.Role != UserRole.lecturer)
                    throw new Exception("Invalid lecturer identifier or user is not a lecturer");
                group.LecturerId = resolvedLecturerId;
            }

            if (!string.IsNullOrWhiteSpace(dto.LeaderId))
            {
                int resolvedLeaderId;
                try { resolvedLeaderId = await ResolveUserIdentifierAsync(dto.LeaderId); }
                catch (KeyNotFoundException) { throw new Exception($"Leader '{dto.LeaderId}' not found. Provide a valid email or numeric user ID."); }

                var leader = await _userRepository.GetByIdAsync(resolvedLeaderId);
                if (leader == null || leader.Role != UserRole.student)
                    throw new Exception("Invalid leader identifier or user is not a student");

                // New leader must already be an active member of this group
                if (!await _memberRepository.IsMemberOfGroupAsync(groupId, resolvedLeaderId))
                    throw new Exception($"Student '{leader.FullName}' is not a member of this group. Add them to the group first before assigning as leader.");

                var oldLeaderId = group.LeaderId;
                group.LeaderId = resolvedLeaderId;

                // Clear is_leader on old leader
                if (oldLeaderId.HasValue && oldLeaderId.Value != resolvedLeaderId)
                {
                    var oldLeaderMember = await _memberRepository.GetByGroupAndUserIdAsync(groupId, oldLeaderId.Value);
                    if (oldLeaderMember != null)
                    {
                        oldLeaderMember.IsLeader = false;
                        await _memberRepository.UpdateAsync(oldLeaderMember);
                    }
                }

                // Set is_leader = true on the new leader's membership row
                var newLeaderMember = await _memberRepository.GetByGroupAndUserIdAsync(groupId, resolvedLeaderId);
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
            if (group == null) return null;
            
            var dto = MapToGroupResponse(group);
            
            var project = await _projectRepository.GetByGroupIdAsync(groupId);
            if (project != null)
            {
                dto.Project = await MapToProjectResponseAsync(project);
            }
            
            return dto;
        }

        public async Task<List<StudentGroupResponseDTO>> GetAllStudentGroupsAsync()
        {
            var groups = await _groupRepository.GetAllAsync();
            var dtos = new List<StudentGroupResponseDTO>();
            
            foreach (var group in groups)
            {
                var dto = MapToGroupResponse(group);
                
                // Add project details if exists
                var project = await _projectRepository.GetByGroupIdAsync(group.GroupId);
                if (project != null)
                {
                    dto.Project = await MapToProjectResponseAsync(project);
                }
                
                dtos.Add(dto);
            }
            
            return dtos;
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

        #region Group Member Management

        public async System.Threading.Tasks.Task RemoveStudentFromGroupAsync(int groupId, string studentIdentifier)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
                throw new Exception("Student group not found");

            int studentId;
            try { studentId = await ResolveUserIdentifierAsync(studentIdentifier); }
            catch (KeyNotFoundException) { throw new Exception($"Student '{studentIdentifier}' not found. Provide a valid email or numeric user ID."); }

            if (!await _memberRepository.IsMemberOfGroupAsync(groupId, studentId))
                throw new Exception("Student is not a member of this group");

            if (group.LeaderId == studentId)
                throw new Exception("Cannot remove the group leader. Assign a different leader first before removing this student.");

            await _memberRepository.RemoveAsync(groupId, studentId);
        }

        public async Task<BulkAddResult> AddStudentsToGroupAsync(int groupId, List<string> studentIdentifiers)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
                throw new Exception("Student group not found");

            var result = new BulkAddResult();

            foreach (var identifier in studentIdentifiers)
            {
                try
                {
                    int studentId;
                    try { studentId = await ResolveUserIdentifierAsync(identifier); }
                    catch (KeyNotFoundException) { throw new Exception($"User '{identifier}' not found. Provide a valid email or numeric user ID."); }

                    var student = await _userRepository.GetByIdAsync(studentId);
                    if (student == null || student.Role != UserRole.student)
                        throw new Exception($"'{identifier}' is not a student account.");

                    if (await _memberRepository.IsMemberOfGroupAsync(groupId, studentId))
                        throw new Exception($"Already a member of this group.");

                    if (await _memberRepository.IsStudentInAnyGroupAsync(studentId))
                        throw new Exception($"Already a member of another group.");

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

        /// <summary>
        /// Resolves a user identifier (numeric ID or email) to an integer user ID.
        /// Throws KeyNotFoundException if the user does not exist.
        /// </summary>
        private async Task<int> ResolveUserIdentifierAsync(string identifier)
        {
            if (int.TryParse(identifier, out var numericId))
            {
                var byId = await _userRepository.GetByIdAsync(numericId);
                if (byId != null) return byId.UserId;
                throw new KeyNotFoundException($"User with ID '{identifier}' not found.");
            }

            var byEmail = await _userRepository.GetByEmailAsync(identifier);
            if (byEmail != null) return byEmail.UserId;
            throw new KeyNotFoundException($"User '{identifier}' not found. Provide a valid email or numeric user ID.");
        }

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
            var activeMembers = group.GroupMembers
                .Where(m => m.LeftAt == null)
                .OrderByDescending(m => m.IsLeader)
                .ThenBy(m => m.User.FullName)
                .Select(m => new GroupMemberResponseDTO
                {
                    MemberId  = m.MembershipId,
                    GroupId   = m.GroupId,
                    UserId    = m.UserId,
                    UserName  = m.User.FullName,
                    Email     = m.User.Email,
                    IsLeader  = m.IsLeader.GetValueOrDefault(false),
                    JoinedAt  = m.JoinedAt,
                    LeftAt    = m.LeftAt
                })
                .ToList();

            return new StudentGroupResponseDTO
            {
                GroupId     = group.GroupId,
                GroupCode   = group.GroupCode,
                GroupName   = group.GroupName,
                LecturerId  = group.LecturerId,
                LecturerName = group.Lecturer?.FullName ?? "Unknown",
                LeaderId    = group.LeaderId,
                LeaderName  = group.Leader?.FullName,
                Status      = group.Status,
                MemberCount = activeMembers.Count,
                Members     = activeMembers,
                CreatedAt   = group.CreatedAt,
                UpdatedAt   = group.UpdatedAt
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

        private async Task<ProjectResponseDTO> MapToProjectResponseAsync(Project project)
        {
            var dto = new ProjectResponseDTO
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

            // Enhanced with integration status (admin group management requirement)
            var jira = await _jiraIntegrationRepository.GetByProjectIdAsync(project.ProjectId);
            if (jira != null)
            {
                dto.JiraStatus = new ProjectIntegrationStatusDTO
                {
                    IsConfigured = true,
                    SyncStatus = jira.SyncStatus.ToString(),
                    LastSync = jira.LastSync,
                    TotalItems = await _jiraIssueRepository.GetCountByProjectIdAsync(project.ProjectId)
                };
            }

            var github = await _githubIntegrationRepository.GetByProjectIdAsync(project.ProjectId);
            if (github != null)
            {
                dto.GithubStatus = new ProjectIntegrationStatusDTO
                {
                    IsConfigured = true,
                    SyncStatus = github.SyncStatus.ToString(),
                    LastSync = github.LastSync,
                    TotalItems = await _githubCommitRepository.GetCountByProjectIdAsync(project.ProjectId)
                };
            }

            return dto;
        }

        private ProjectResponseDTO MapToProjectResponse(Project project)
        {
            // Sync wrapper for Async map
            return MapToProjectResponseAsync(project).GetAwaiter().GetResult();
        }

        #endregion
    }
}
