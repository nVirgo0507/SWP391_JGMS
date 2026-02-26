﻿using BLL.DTOs.Admin;
using BLL.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SWP391_JGMS.Controllers
{
    /// <summary>
    /// SECURITY WARNING: This controller currently has NO authentication or authorization.
    /// Any user can access these endpoints. Before deploying to production:
    /// 1. Implement JWT authentication in Program.cs
    /// 2. Add [Authorize(Roles = "admin")] attribute to this controller or individual endpoints
    /// 3. Configure authentication middleware
    /// See: https://learn.microsoft.com/en-us/aspnet/core/security/authentication/
    /// </summary>
    [ApiController]
	[Authorize(Roles = "admin")]
	[Route("api/admin")]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _adminService;

        public AdminController(IAdminService adminService)
        {
            _adminService = adminService;
        }

        #region User Management

        /// <summary>
        /// BR-053: Admin Full Access - Create a new user (admin, lecturer, or student)
        /// BR-001: Unique Email Address
        /// BR-005: Password Strength
        /// BR-006: Active Status Default
        /// </summary>
        [HttpPost("users")]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserDTO dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var user = await _adminService.CreateUserAsync(dto);
                return CreatedAtAction(nameof(GetUserById), new { userId = user.UserId }, user);
            }
            catch (Exception ex)
            {
                var errorMessage = ex.Message;
                if (ex.InnerException != null)
                {
                    errorMessage += " | Inner: " + ex.InnerException.Message;
                }
                return BadRequest(new { message = errorMessage, stackTrace = ex.StackTrace });
            }
        }

        /// <summary>
        /// BR-053: Admin Full Access - Update user information
        /// BR-060: Preserve Audit Trail
        /// </summary>
        [HttpPut("users/{userId}")]
        public async Task<IActionResult> UpdateUser(int userId, [FromBody] UpdateUserDTO dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var user = await _adminService.UpdateUserAsync(userId, dto);
                return Ok(user);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// BR-053: Admin Full Access - Get user by ID
        /// </summary>
        [HttpGet("users/{userId}")]
        public async Task<IActionResult> GetUserById(int userId)
        {
            try
            {
                var user = await _adminService.GetUserByIdAsync(userId);
                if (user == null)
                {
                    return NotFound(new { message = "User not found" });
                }
                return Ok(user);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// BR-053: Admin Full Access - Get all users
        /// </summary>
        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers()
        {
            try
            {
                var users = await _adminService.GetAllUsersAsync();
                return Ok(users);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// BR-053: Admin Full Access - Get users by role
        /// </summary>
        [HttpGet("users/role/{role}")]
        public async Task<IActionResult> GetUsersByRole(string role)
        {
            try
            {
                var users = await _adminService.GetUsersByRoleAsync(role);
                return Ok(users);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// BR-053: Admin Full Access - Delete user
        /// BR-059: Cascade Delete Prevention
        /// </summary>
        [HttpDelete("users/{userId}")]
        public async Task<IActionResult> DeleteUser(int userId)
        {
            try
            {
                await _adminService.DeleteUserAsync(userId);
                return Ok(new { message = "User deleted successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// BR-053: Admin Full Access - Set user status
        /// BR-007: Inactive Users Cannot Login
        /// </summary>
        [HttpPatch("users/{userId}/status")]
        public async Task<IActionResult> SetUserStatus(int userId, [FromBody] SetUserStatusDTO dto)
        {
            try
            {
                await _adminService.SetUserStatusAsync(userId, dto.Status);
                return Ok(new { message = "User status updated successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        #endregion

        #region Student Group Management

        /// <summary>
        /// BR-053: Admin Full Access - Create a new student group
        /// </summary>
        [HttpPost("groups")]
        public async Task<IActionResult> CreateStudentGroup([FromBody] CreateStudentGroupDTO dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var group = await _adminService.CreateStudentGroupAsync(dto);
                return CreatedAtAction(nameof(GetStudentGroupById), new { groupId = group.GroupId }, group);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// BR-053: Admin Full Access - Update student group
        /// BR-060: Preserve Audit Trail
        /// </summary>
        [HttpPut("groups/{groupId}")]
        public async Task<IActionResult> UpdateStudentGroup(int groupId, [FromBody] UpdateStudentGroupDTO dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var group = await _adminService.UpdateStudentGroupAsync(groupId, dto);
                return Ok(group);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// BR-053: Admin Full Access - Get student group by ID
        /// </summary>
        [HttpGet("groups/{groupId}")]
        public async Task<IActionResult> GetStudentGroupById(int groupId)
        {
            try
            {
                var group = await _adminService.GetStudentGroupByIdAsync(groupId);
                if (group == null)
                {
                    return NotFound(new { message = "Student group not found" });
                }
                return Ok(group);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// BR-053: Admin Full Access - Get all student groups
        /// </summary>
        [HttpGet("groups")]
        public async Task<IActionResult> GetAllStudentGroups()
        {
            try
            {
                var groups = await _adminService.GetAllStudentGroupsAsync();
                return Ok(groups);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// BR-053: Admin Full Access - Delete student group
        /// BR-061: Soft Delete for Groups
        /// </summary>
        [HttpDelete("groups/{groupId}")]
        public async Task<IActionResult> DeleteStudentGroup(int groupId)
        {
            try
            {
                await _adminService.DeleteStudentGroupAsync(groupId);
                return Ok(new { message = "Student group deleted successfully (soft delete)" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        #endregion

        #region Lecturer Management

        /// <summary>
        /// BR-053: Admin Full Access - Get all lecturers
        /// </summary>
        [HttpGet("lecturers")]
        public async Task<IActionResult> GetAllLecturers()
        {
            try
            {
                var lecturers = await _adminService.GetAllLecturersAsync();
                return Ok(lecturers);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// BR-053: Admin Full Access - Assign lecturer to group
        /// </summary>
        [HttpPut("groups/{groupId}/lecturer")]
        public async Task<IActionResult> AssignLecturerToGroup(int groupId, [FromBody] AssignLecturerDTO dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                await _adminService.AssignLecturerToGroupAsync(groupId, dto.LecturerId);
                return Ok(new { message = "Lecturer assigned to group successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        #endregion

        #region Group Member Management

        /// <summary>
        /// BR-053: Admin Full Access - Add student to group
        /// </summary>
        [HttpPost("groups/{groupId}/members/{studentId}")]
        public async Task<IActionResult> AddStudentToGroup(int groupId, int studentId)
        {
            try
            {
                await _adminService.AddStudentToGroupAsync(groupId, studentId);
                return Ok(new { message = "Student added to group successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// BR-053: Admin Full Access - Remove student from group
        /// </summary>
        [HttpDelete("groups/{groupId}/members/{studentId}")]
        public async Task<IActionResult> RemoveStudentFromGroup(int groupId, int studentId)
        {
            try
            {
                await _adminService.RemoveStudentFromGroupAsync(groupId, studentId);
                return Ok(new { message = "Student removed from group successfully" });
            }
            catch (Exception ex)
            {
                var errorMessage = ex.Message;
                if (ex.InnerException != null)
                {
                    errorMessage += " | Inner: " + ex.InnerException.Message;
                }
                return BadRequest(new { message = errorMessage });
            }
        }

        #endregion
    }
}
