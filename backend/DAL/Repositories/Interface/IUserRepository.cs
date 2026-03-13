﻿using DAL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Repositories.Interface
{
	public interface IUserRepository
	{
		Task<User?> GetByEmailAsync(string email);
		Task<User?> GetByGithubUsernameAsync(string username);
		System.Threading.Tasks.Task AddAsync(User user);
		Task<bool> EmailExistsAsync(string email);
		Task<bool> PhoneExistsAsync(string phone);
		Task<bool> GithubUsernameExistsAsync(string githubUsername);
		Task<bool> JiraAccountIdExistsAsync(string jiraAccountId);

		// Admin methods
		Task<User?> GetByIdAsync(int userId);
		Task<List<User>> GetAllAsync();
		Task<List<User>> GetByRoleAsync(UserRole role);
		Task<List<User>> SearchByNameOrEmailAsync(string query, UserRole? role = null);
		/// <summary>Returns active students who are not currently a member of any group.</summary>
		Task<List<User>> GetAvailableStudentsAsync();
		System.Threading.Tasks.Task UpdateAsync(User user);
		System.Threading.Tasks.Task DeleteAsync(int userId);
		Task<bool> StudentCodeExistsAsync(string studentCode);
		Task<bool> CanDeleteUserAsync(int userId);
	}
}
