using BLL.DTOs;
using BLL.Services.Interface;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
namespace SWP391_JGMS.Controllers
{
	[ApiController]
	[Route("api/auth")]
	[Produces("application/json")]
	public class AuthController : ControllerBase
	{
		private readonly IUserService _userService;
		private readonly IAtlassianAuthService _atlassianAuthService;

		public AuthController(IUserService userService, IAtlassianAuthService atlassianAuthService)
		{
			_userService = userService;
			_atlassianAuthService = atlassianAuthService;
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
				return BadRequest(new { message = ex.InnerException?.Message ?? ex.Message });
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
				return BadRequest(new { message = ex.InnerException?.Message ?? ex.Message });
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
				return BadRequest(new { message = ex.InnerException?.Message ?? ex.Message });
			}
		}

        public class JiraSsoCallbackRequest
        {
            public string Code { get; set; } = null!;
        }

		[HttpPost("sso/jira/callback")]
		public async Task<IActionResult> JiraSsoCallback([FromBody] JiraSsoCallbackRequest request)
		{
			try
			{
				var tokens = await _atlassianAuthService.ExchangeCodeForTokensAsync(request.Code);
                var profile = await _atlassianAuthService.GetAtlassianProfileAsync(tokens.AccessToken);
                var result = await _userService.HandleJiraSsoAsync(profile, tokens.AccessToken, tokens.RefreshToken, tokens.ExpiresIn);

				return Ok(result);
			}
			catch (Exception ex)
			{
				return BadRequest(new { message = ex.InnerException?.Message ?? ex.Message });
			}
		}

		[HttpPost("sso/jira/register")]
		public async Task<IActionResult> JiraSsoRegister([FromBody] SsoRegisterDTO dto)
		{
			try
			{
				if (!ModelState.IsValid)
					return BadRequest(ModelState);

				var token = await _userService.RegisterJiraSsoAsync(dto);
				return Ok(new { accessToken = token });
			}
			catch (Exception ex)
			{
				return BadRequest(new { message = ex.InnerException?.Message ?? ex.Message });
			}
		}

		/// <summary>
		/// Change user password. Requires authentication.
		/// </summary>
		[HttpPut("change-password")]
		[Authorize]
		public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDTO dto)
		{
			try
			{
				if (!ModelState.IsValid)
					return BadRequest(ModelState);

				var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
				if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
					return Unauthorized(new { message = "Invalid token claims" });

				await _userService.ChangePasswordAsync(userId, dto);
				return Ok(new { message = "Password changed successfully" });
			}
			catch (Exception ex)
			{
				return BadRequest(new { message = ex.InnerException?.Message ?? ex.Message });
			}
		}
	}
}
