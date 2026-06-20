using System.Collections.Generic;
using System.Threading.Tasks;
using DAL.Models;

namespace BLL.Services.Interface
{
    public interface IAiChatService
    {
        Task<string> SendMessageAsync(int userId, string message);
        Task<IEnumerable<ChatMessage>> GetChatHistoryAsync(int userId);
    }
}
