using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.DTOs
{
	/// <summary>
	/// BR-001: Request DTO for user registration
	/// BR-005: Password must meet strength requirements
	/// BR-006: New users default to active status
	/// </summary>
	public class RegisterDTO
	{
		[Required]
		[EmailAddress]
		public string Email { get; set; } = null!;

		[Required]
		public string Password { get; set; } = null!;

		[Required]
		public string FullName { get; set; } = null!;

		[Required]
		public string Phone { get; set; } = null!;

		[RegularExpression(@"^SE\d{6}$", ErrorMessage = "Student code must start with 'SE' followed by exactly 6 digits (e.g. SE123456)")]
		public string? StudentCode { get; set; }

		[RegularExpression(@"^[a-zA-Z0-9]([a-zA-Z0-9-]{0,37}[a-zA-Z0-9]|[a-zA-Z0-9])?$",
			ErrorMessage = "GitHub username must be alphanumeric with hyphens, 1-39 characters, and cannot start or end with a hyphen")]
		public string? GithubUsername { get; set; }

		public string? JiraAccountId { get; set; }
	}
}
