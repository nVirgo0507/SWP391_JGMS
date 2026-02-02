using DAL.Models;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Data
{
	public static class DbInitializer
	{
		public static void SeedAdmin(JgmsContext context)
    {
        if (context.Users.Any(u => u.Email == "admin@swp391.edu.vn"))
            return;

        var hasher = new PasswordHasher<User>();

        var admin = new User
        {
            Email = "admin@swp391.edu.vn",
            Role = UserRole.admin,
            FullName = "System Administrator",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Status = UserStatus.active
		};

		admin.PasswordHash = hasher.HashPassword(admin, "123456");

		context.Users.Add(admin);
		context.SaveChanges();
		}
	}
}
