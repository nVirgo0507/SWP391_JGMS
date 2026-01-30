using DAL.Models;
using DAL.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repositories
{
    public class GroupMemberRepository : IGroupMemberRepository
    {
        private readonly JgmsContext _context;

        public GroupMemberRepository(JgmsContext context)
        {
            _context = context;
        }

        public async Task<List<GroupMember>> GetByGroupIdAsync(int groupId)
        {
            return await _context.GroupMembers
                .Include(gm => gm.User)
                .Where(gm => gm.GroupId == groupId)
                .ToListAsync();
        }

        public async Task<GroupMember?> GetByGroupAndUserIdAsync(int groupId, int userId)
        {
            return await _context.GroupMembers
                .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == userId);
        }

        public async System.Threading.Tasks.Task AddAsync(GroupMember member)
        {
            member.JoinedAt = DateTime.UtcNow;
            _context.GroupMembers.Add(member);
            await _context.SaveChangesAsync();
        }

        public async System.Threading.Tasks.Task RemoveAsync(int groupId, int userId)
        {
            var member = await GetByGroupAndUserIdAsync(groupId, userId);
            if (member != null)
            {
                _context.GroupMembers.Remove(member);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> IsMemberOfGroupAsync(int groupId, int userId)
        {
            return await _context.GroupMembers
                .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId);
        }
    }
}
