﻿using System;
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
	}
}
