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
	public interface IUserService
	{
		System.Threading.Tasks.Task RegisterAsync(RegisterDTO dto);
		Task<User?> LoginAsync(LoginDTO dto);
	}
}
