using BLL.DTOs;
using DAL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

namespace BLL.Services.Interface
{
	/// <summary>
	/// User Authentication Service Interface
	/// Handles user registration and login functionality
	/// </summary>
	public interface IUserService
	{
		/// <summary>
		/// BR-001: Register a new user with unique email validation
		/// BR-005: Password Strength validation
		/// BR-006: Active Status Default - New users are set to active status
		/// </summary>
		System.Threading.Tasks.Task RegisterAsync(RegisterDTO dto);

		/// <summary>
		/// BR-007: Inactive Users Cannot Login - Validates user is active before login
		/// Authenticates user with email and password
		/// Returns user object if credentials are valid, null otherwise
		/// </summary>
		Task<User?> LoginAsync(LoginDTO dto);
	}
}
