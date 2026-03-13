using BLL.DTOs;
using BLL.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace SWP391_JGMS.Controllers
{
	[ApiController]
	[Route("api/auth")]
	[Produces("application/json")]
	public class AuthController : ControllerBase
	{
		private readonly IUserService _userService;
		public AuthController(IUserService userService)
		{
			_userService = userService;
		}

		/// <summary>
		/// Register a new student account.
		/// </summary>
		[HttpPost("register")]
		public async Task<IActionResult> Register([FromBody]RegisterDTO dto)
		{
			try
			{
				if (!ModelState.IsValid)
					return BadRequest(ModelState);

				await _userService.RegisterAsync(dto);
				return Ok(new { message = "Register success" });
			}
			catch (Exception ex)
			{
				return BadRequest(new { message = ex.Message });
			}
		}

		/// <summary>
		/// Register a new lecturer account.
		/// Does not require student code, GitHub username, or Jira account ID.
		/// </summary>
		[HttpPost("register/lecturer")]
		public async Task<IActionResult> RegisterLecturer([FromBody] RegisterLecturerDTO dto)
		{
			try
			{
				if (!ModelState.IsValid)
					return BadRequest(ModelState);

				await _userService.RegisterLecturerAsync(dto);
				return Ok(new { message = "Lecturer registered successfully" });
			}
			catch (Exception ex)
			{
				return BadRequest(new { message = ex.Message });
			}
		}

		/// <summary>
		/// Login with email and password. Returns a JWT access token.
		/// </summary>
		[HttpPost("login")]
		public async Task<IActionResult> Login([FromBody] LoginDTO dto)
		{
			try
			{
				var token = await _userService.LoginAsync(dto);

				if (token == null)
					return Unauthorized(new { message = "Invalid email or password" });

				return Ok(new { accessToken = token });
			}
			catch (Exception ex)
			{
				return BadRequest(new { message = ex.Message });
			}
		}
	}
}
