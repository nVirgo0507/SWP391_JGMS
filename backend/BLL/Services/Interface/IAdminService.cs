﻿using BLL.DTOs.Admin;

namespace BLL.Services.Interface
{
    /// <summary>
    /// BR-053: Admin Full Access - Admins have full control over all users, groups, and system resources
    /// </summary>
    public interface IAdminService
    {
        #region User Management

        /// <summary>
        /// BR-053: Create a new user (admin, lecturer, or student)
        /// BR-001: Unique Email Address validation
        /// BR-002: Role Assignment validation
        /// BR-005: Password Strength validation
        /// </summary>
        Task<UserResponseDTO> CreateUserAsync(CreateUserDTO dto);

        /// <summary>
        /// BR-060: Preserve Audit Trail - Update user details
        /// BR-001: Unique Email Address validation (if email changed)
        /// BR-002: Role Assignment validation (if role changed)
        /// </summary>
        Task<UserResponseDTO> UpdateUserAsync(int userId, UpdateUserDTO dto);

        /// <summary>
        /// BR-053: Get user by ID
        /// </summary>
        Task<UserResponseDTO?> GetUserByIdAsync(int userId);

        /// <summary>
        /// BR-053: Get all users in the system
        /// </summary>
        Task<List<UserResponseDTO>> GetAllUsersAsync();

        /// <summary>
        /// BR-002: Get users filtered by role (admin, lecturer, student)
        /// </summary>
        Task<List<UserResponseDTO>> GetUsersByRoleAsync(string role);

        /// <summary>
        /// Search users by name or email, optionally filtered by role.
        /// Returns matching users ordered by name.
        /// </summary>
        Task<List<UserResponseDTO>> SearchUsersAsync(string query, string? role = null);

        /// <summary>
        /// Returns all active students who are not currently a member of any group.
        /// Use this to populate the student picker when creating or filling a group.
        /// </summary>
        Task<List<UserResponseDTO>> GetAvailableStudentsAsync();

        /// <summary>
        /// BR-059: Cascade Delete Prevention - Delete user with validation
        /// Cannot delete if user has associated data
        /// </summary>
        System.Threading.Tasks.Task DeleteUserAsync(int userId);

        /// <summary>
        /// BR-007: Inactive Users Cannot Login - Set user status
        /// </summary>
        System.Threading.Tasks.Task SetUserStatusAsync(int userId, string status);

        #endregion

        #region Student Group Management

        /// <summary>
        /// BR-053: Create a new student group
        /// Validates lecturer and leader exist and have correct roles
        /// </summary>
        Task<StudentGroupResponseDTO> CreateStudentGroupAsync(CreateStudentGroupDTO dto);

        /// <summary>
        /// BR-060: Preserve Audit Trail - Update group details
        /// </summary>
        Task<StudentGroupResponseDTO> UpdateStudentGroupAsync(int groupId, UpdateStudentGroupDTO dto);

        /// <summary>
        /// BR-053: Get group by ID
        /// </summary>
        Task<StudentGroupResponseDTO?> GetStudentGroupByIdAsync(int groupId);

        /// <summary>
        /// BR-053: Get all student groups
        /// </summary>
        Task<List<StudentGroupResponseDTO>> GetAllStudentGroupsAsync();

        /// <summary>
        /// BR-061: Soft Delete for Groups - Delete group with validation
        /// Cannot delete if group has associated project
        /// </summary>
        System.Threading.Tasks.Task DeleteStudentGroupAsync(int groupId);

        #endregion

        #region Group Member Management

        /// <summary>
        /// Add multiple students to a group in one call.
        /// Each identifier can be an email address or a numeric user ID.
        /// Returns a summary of successes and failures.
        /// </summary>
        Task<BulkAddResult> AddStudentsToGroupAsync(int groupId, List<string> studentIdentifiers);

        /// <summary>
        /// BR-053: Remove a student from a group.
        /// Accepts numeric student ID or email address.
        /// </summary>
        System.Threading.Tasks.Task RemoveStudentFromGroupAsync(int groupId, string studentIdentifier);

        /// <summary>
        /// Remove all members from a group when its project is marked as completed.
        /// Clears the group leader as well.
        /// </summary>
        System.Threading.Tasks.Task ClearGroupMembersOnCompletionAsync(int groupId);

        #endregion

        #region Project Management

        /// <summary>
        /// Create a project linked to a group. Each group can only have one project.
        /// </summary>
        Task<ProjectResponseDTO> CreateProjectAsync(int groupId, CreateProjectDTO dto);

        /// <summary>
        /// Update an existing project.
        /// </summary>
        Task<ProjectResponseDTO> UpdateProjectAsync(int projectId, UpdateProjectDTO dto);

        /// <summary>
        /// Get a project by its ID.
        /// </summary>
        Task<ProjectResponseDTO?> GetProjectByIdAsync(int projectId);

        /// <summary>
        /// Get the project for a specific group.
        /// </summary>
        Task<ProjectResponseDTO?> GetProjectByGroupIdAsync(int groupId);

        /// <summary>
        /// Get all projects.
        /// </summary>
        Task<List<ProjectResponseDTO>> GetAllProjectsAsync();

        /// <summary>
        /// Delete a project.
        /// </summary>
        System.Threading.Tasks.Task DeleteProjectAsync(int projectId);

        #endregion
    }
}
