using System;

namespace BLL.DTOs
{
	public class ChangePasswordDTO
	{
		public string CurrentPassword { get; set; } = null!;
		public string NewPassword { get; set; } = null!;
	}
}
