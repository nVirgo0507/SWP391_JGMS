using BLL.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.Tasks;
using System.Linq;

namespace SWP391_JGMS.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly IAiChatService _chatService;

        public ChatController(IAiChatService chatService)
        {
            _chatService = chatService;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (int.TryParse(userIdClaim, out var id)) return id;
            throw new System.UnauthorizedAccessException("Invalid or missing user identity in token.");
        }

        public class ChatRequest
        {
            public string Message { get; set; } = string.Empty;
        }

        [HttpPost]
        public async Task<IActionResult> Chat([FromBody] ChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new { Error = "Message cannot be empty." });
            }

            var userId = GetCurrentUserId();
            var reply = await _chatService.SendMessageAsync(userId, request.Message);
            return Ok(new { Reply = reply });
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetHistory()
        {
            var userId = GetCurrentUserId();
            var history = await _chatService.GetChatHistoryAsync(userId);
            
            var formattedHistory = history.Select(h => new {
                h.Id,
                h.Message,
                h.Reply,
                h.CreatedAt
            });

            return Ok(formattedHistory);
        }
    }
}
