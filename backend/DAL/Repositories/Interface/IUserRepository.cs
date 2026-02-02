using DAL.Models;
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
		System.Threading.Tasks.Task AddAsync(User user);
		Task<bool> EmailExistsAsync(string email);
		
		// Admin methods
		Task<User?> GetByIdAsync(int userId);
		Task<List<User>> GetAllAsync();
		Task<List<User>> GetByRoleAsync(UserRole role);
		System.Threading.Tasks.Task UpdateAsync(User user);
		System.Threading.Tasks.Task DeleteAsync(int userId);
		Task<bool> StudentCodeExistsAsync(string studentCode);
		Task<bool> CanDeleteUserAsync(int userId);
	}
}
