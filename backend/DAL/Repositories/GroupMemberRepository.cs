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
                .Where(gm => gm.GroupId == groupId && gm.LeftAt == null)
                .ToListAsync();
        }

        public async Task<List<GroupMember>> GetGroupsByStudentIdAsync(int userId)
        {
            return await _context.GroupMembers
                .Include(gm => gm.Group)
                    .ThenInclude(g => g.Project)
                .Where(gm => gm.UserId == userId && gm.LeftAt == null && gm.Group.Status == UserStatus.active)
                .ToListAsync();
        }

        public async Task<GroupMember?> GetByGroupAndUserIdAsync(int groupId, int userId)
        {
            return await _context.GroupMembers
                .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == userId && gm.LeftAt == null);
        }

        public async Task<GroupMember?> GetPreviousMembershipAsync(int groupId, int userId)
        {
            // Find the most recent soft-deleted membership for this student in this group
            return await _context.GroupMembers
                .Where(gm => gm.GroupId == groupId && gm.UserId == userId && gm.LeftAt != null)
                .OrderByDescending(gm => gm.LeftAt)
                .FirstOrDefaultAsync();
        }

        public async System.Threading.Tasks.Task AddAsync(GroupMember member)
        {
            member.JoinedAt = DateTime.UtcNow;
            _context.GroupMembers.Add(member);
            await _context.SaveChangesAsync();
        }

        public async System.Threading.Tasks.Task RejoinAsync(GroupMember existingMembership)
        {
            // Re-activate a previously soft-deleted membership
            existingMembership.JoinedAt = DateTime.UtcNow;
            existingMembership.LeftAt = null;
            existingMembership.IsLeader = false;
            _context.GroupMembers.Update(existingMembership);
            await _context.SaveChangesAsync();
        }

        public async System.Threading.Tasks.Task UpdateAsync(GroupMember member)
        {
            _context.GroupMembers.Update(member);
            await _context.SaveChangesAsync();
        }

        public async System.Threading.Tasks.Task RemoveAsync(int groupId, int userId)
        {
            var member = await GetByGroupAndUserIdAsync(groupId, userId);
            if (member != null)
            {
                member.LeftAt = DateTime.UtcNow;
                member.IsLeader = false;
                _context.GroupMembers.Update(member);
                await _context.SaveChangesAsync();
            }
        }

        public async System.Threading.Tasks.Task RemoveAllMembersAsync(int groupId)
        {
            var members = await _context.GroupMembers
                .Where(gm => gm.GroupId == groupId && gm.LeftAt == null)
                .ToListAsync();

            if (members.Count > 0)
            {
                var now = DateTime.UtcNow;
                foreach (var m in members)
                {
                    m.LeftAt = now;
                    m.IsLeader = false;
                }
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> IsMemberOfGroupAsync(int groupId, int userId)
        {
            return await _context.GroupMembers
                .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId
                                && gm.LeftAt == null && gm.Group.Status == UserStatus.active);
        }

        public async Task<bool> IsStudentInAnyGroupAsync(int userId)
        {
            return await _context.GroupMembers
                .AnyAsync(gm => gm.UserId == userId && gm.LeftAt == null && gm.Group.Status == UserStatus.active);
        }
    }
}
