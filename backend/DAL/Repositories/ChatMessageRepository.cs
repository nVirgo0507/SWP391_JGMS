using DAL.Data;
using DAL.Models;
using DAL.Repositories.Interface;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DAL.Repositories
{
    public class ChatMessageRepository : IChatMessageRepository
    {
        private readonly JgmsContext _context;

        public ChatMessageRepository(JgmsContext context)
        {
            _context = context;
        }

        public async Task<ChatMessage> AddAsync(ChatMessage chatMessage)
        {
            await _context.ChatMessages.AddAsync(chatMessage);
            await _context.SaveChangesAsync();
            return chatMessage;
        }

        public async Task<IEnumerable<ChatMessage>> GetByUserIdAsync(int userId)
        {
            return await _context.ChatMessages
                .Where(c => c.UserId == userId)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();
        }
    }
}