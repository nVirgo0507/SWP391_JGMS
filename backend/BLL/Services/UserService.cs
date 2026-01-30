using BLL.DTOs;
using BLL.Services.Interface;
using DAL.Models;
using DAL.Repositories.Interface;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BLL.Services
{
	public class UserService : IUserService
	{
		private readonly IUserRepository _userRepository;
		private readonly PasswordHasher<User> _passwordHasher;
		public UserService(IUserRepository userRepository) 
		{
			_userRepository = userRepository;
			_passwordHasher = new PasswordHasher<User>();
		}
		public async Task<User?> LoginAsync(LoginDTO dto)
		{
			var user = await  _userRepository.GetByEmailAsync(dto.Email);
			if (user == null)
			{
				return null;
			}

			var result = _passwordHasher.VerifyHashedPassword(
				user,
				user.PasswordHash,
				dto.Password
			);

			if (result == PasswordVerificationResult.Success)
			{
				return user;
			}

			return null;
		}

		public async System.Threading.Tasks.Task RegisterAsync(RegisterDTO dto)
		{
			if (await _userRepository.EmailExistsAsync(dto.Email))
			{
				throw new Exception("Email address already exists in the system");
			}

			var regex = new Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$");
			if (!regex.IsMatch(dto.Password))
			{
				throw new Exception("Password must be at least 8 characters with uppercase, lowercase, and number");
			}

			var user = new User
			{
				Email = dto.Email,
				FullName = dto.FullName,
				StudentCode = dto.StudentCode,
				Role = UserRole.student,
				Status = UserStatus.active,
				CreatedAt = DateTime.Now,
				UpdatedAt = DateTime.Now
			};

			user.PasswordHash = _passwordHasher.HashPassword(user, dto.Password);
			await _userRepository.AddAsync(user);
		}
	}
}
