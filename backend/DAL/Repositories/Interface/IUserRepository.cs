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
	}
}
