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
