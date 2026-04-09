﻿using DAL.Models;
using DAL.Repositories.Interface;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Repositories
{
	public class UserRepository : IUserRepository
	{
		private readonly JgmsContext _context;
		public UserRepository(JgmsContext context)
		{
			_context = context;
		}
		public async System.Threading.Tasks.Task AddAsync(User user)
		{
			_context.Users.Add(user);
			await _context.SaveChangesAsync();
		}

		public async Task<bool> EmailExistsAsync(string email)
		{
			var normalized = email.Trim().ToLower();
			return await _context.Users.AnyAsync(x => x.Email.ToLower() == normalized);
		}

		public async Task<bool> PhoneExistsAsync(string phone)
		{
			return await _context.Users.AnyAsync(x => x.Phone == phone);
		}

		public async Task<bool> GithubUsernameExistsAsync(string githubUsername)
		{
			var normalized = githubUsername.Trim().ToLower();
			return await _context.Users.AnyAsync(x => x.GithubUsername != null && x.GithubUsername.ToLower() == normalized);
		}

		public async Task<bool> JiraAccountIdExistsAsync(string jiraAccountId)
		{
			return await _context.Users.AnyAsync(x => x.JiraAccountId == jiraAccountId);
		}

		public async Task<User?> GetByJiraAccountIdAsync(string jiraAccountId)
		{
			return await _context.Users.FirstOrDefaultAsync(x => x.JiraAccountId == jiraAccountId);
		}

		public async Task<User?> GetByEmailAsync(string email)
		{
			var normalized = email.Trim().ToLower();
			return await _context.Users.FirstOrDefaultAsync(x => x.Email.ToLower() == normalized);
		}

		public async Task<User?> GetByGithubUsernameAsync(string username)
		{
			var normalized = username.Trim().ToLower();
			return await _context.Users.FirstOrDefaultAsync(x => x.GithubUsername != null && x.GithubUsername.ToLower() == normalized);
		}

		public async Task<User?> GetByIdAsync(int userId)
		{
			return await _context.Users.FirstOrDefaultAsync(x => x.UserId == userId);
		}

		public async Task<List<User>> GetAllAsync()
		{
			return await _context.Users.ToListAsync();
		}

		public async Task<List<User>> GetByRoleAsync(UserRole role)
		{
			return await _context.Users.Where(x => x.Role == role).ToListAsync();
		}

		public async Task<List<User>> SearchByNameOrEmailAsync(string query, UserRole? role = null)
		{
			var q = query.ToLower();
			var results = _context.Users
				.Where(u => u.FullName.ToLower().Contains(q) || u.Email.ToLower().Contains(q));
			if (role.HasValue)
				results = results.Where(u => u.Role == role.Value);
			return await results.OrderBy(u => u.FullName).ToListAsync();
		}

		public async Task<List<User>> GetAvailableStudentsAsync()
		{
			// Students with role=student, status=active, and no active (LeftAt == null) group membership
			var occupiedStudentIds = await _context.GroupMembers
				.Where(gm => gm.LeftAt == null)
				.Select(gm => gm.UserId)
				.Distinct()
				.ToListAsync();

			return await _context.Users
				.Where(u => u.Role == UserRole.student
						 && u.Status == UserStatus.active
						 && !occupiedStudentIds.Contains(u.UserId))
				.OrderBy(u => u.FullName)
				.ToListAsync();
		}

		public async System.Threading.Tasks.Task UpdateAsync(User user)
		{
			user.UpdatedAt = DateTime.UtcNow;
			_context.Users.Update(user);
			await _context.SaveChangesAsync();
		}

		public async System.Threading.Tasks.Task DeleteAsync(int userId)
		{
			var user = await GetByIdAsync(userId);
			if (user != null)
			{
				_context.Users.Remove(user);
				await _context.SaveChangesAsync();
			}
		}

		public async Task<bool> StudentCodeExistsAsync(string studentCode)
		{
			return await _context.Users.AnyAsync(x => x.StudentCode == studentCode);
		}

		public async Task<bool> CanDeleteUserAsync(int userId)
		{
			// Check if user is referenced in critical tables
			var hasGroups = await _context.StudentGroups.AnyAsync(g => g.LecturerId == userId || g.LeaderId == userId);
			var hasGroupMembers = await _context.GroupMembers.AnyAsync(gm => gm.UserId == userId);
			var hasRequirements = await _context.Requirements.AnyAsync(r => r.CreatedBy == userId);
			var hasTasks = await _context.Tasks.AnyAsync(t => t.AssignedTo == userId);

			return !hasGroups && !hasGroupMembers && !hasRequirements && !hasTasks;
		}
	}
}
