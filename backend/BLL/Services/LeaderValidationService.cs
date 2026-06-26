using System;
using System.Threading.Tasks;
using BLL.Services.Interface;
using DAL.Repositories.Interface;

namespace BLL.Services
{
    public class LeaderValidationService : ILeaderValidationService
    {
        private readonly IGroupMemberRepository _memberRepository;

        public LeaderValidationService(IGroupMemberRepository memberRepository)
        {
            _memberRepository = memberRepository;
        }

        public async Task ValidateLeaderAccessAsync(int userId, int groupId)
        {
            var groupMember = await _memberRepository.GetByGroupAndUserIdAsync(groupId, userId);

            if (groupMember == null || !groupMember.IsLeader.GetValueOrDefault(false))
            {
                throw new Exception("Access denied. You are not the leader of this group.");
            }
        }
    }
}
