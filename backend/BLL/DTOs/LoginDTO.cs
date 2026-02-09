using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.DTOs
{
	/// <summary>
	/// BR-007: Request DTO for user login
	/// BR-007: Inactive users cannot login - validation happens in UserService
	/// </summary>
	public class LoginDTO
	{
		public string Email { get; set; } = null!;
		public string Password { get; set; } = null!;
	}
}
