using BLL.DTOs;
using BLL.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace SWP391_JGMS.Controllers
{
	[ApiController]
	[Route("api/auth")]
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
			if (!ModelState.IsValid)
			{
				return BadRequest(ModelState);
			}
				
			await _userService.RegisterAsync(dto);
			return Ok("Register success");
		}

		[HttpPost("login")]
		public async Task<IActionResult> Login(LoginDTO dto)
		{
			var token = await _userService.LoginAsync(dto);

			if (token == null)
				return Unauthorized("Invalid email or password");

			return Ok(new
			{
				accessToken = token
			});
		}
	}
}
