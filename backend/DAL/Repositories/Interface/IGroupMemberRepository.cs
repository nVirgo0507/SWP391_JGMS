﻿﻿using DAL.Models;

namespace DAL.Repositories.Interface
{
    public interface IGroupMemberRepository
    {
        Task<List<GroupMember>> GetByGroupIdAsync(int groupId);
        Task<List<GroupMember>> GetGroupsByStudentIdAsync(int userId);
        Task<GroupMember?> GetByGroupAndUserIdAsync(int groupId, int userId);
        System.Threading.Tasks.Task AddAsync(GroupMember member);
        System.Threading.Tasks.Task UpdateAsync(GroupMember member);
        System.Threading.Tasks.Task RemoveAsync(int groupId, int userId);
        Task<bool> IsMemberOfGroupAsync(int groupId, int userId);
    }
}
