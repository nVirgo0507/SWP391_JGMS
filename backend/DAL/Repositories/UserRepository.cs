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
			return await _context.Users.AnyAsync(x => x.Email == email);
		}

		public async Task<User?> GetByEmailAsync(string email)
		{
			return await _context.Users.FirstOrDefaultAsync(x => x.Email == email);
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
