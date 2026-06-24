using DAL.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DAL.Repositories.Interface
{
    public interface IChatMessageRepository
    {
        Task<ChatMessage> AddAsync(ChatMessage chatMessage);
        Task<IEnumerable<ChatMessage>> GetByUserIdAsync(int userId);
    }
}