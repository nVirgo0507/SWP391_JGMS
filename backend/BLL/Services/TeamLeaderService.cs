using BLL.DTOs.Admin;
using BLL.Services.Interface;
using DAL.Models;
using DAL.Repositories.Interface;
using System.Threading.Tasks;

namespace BLL.Services
{
    /// <summary>
    /// Service for team leader-scoped operations with group access control
    /// BR-055: Team Leader Group-Scoped Access - Team leaders can only manage their own group's project
    /// Validation: Check user is leader of the group via GROUP_MEMBER.is_leader
    /// Error Message: "Access denied. You are not the leader of this group."
    /// </summary>
    public class TeamLeaderService : ITeamLeaderService
    {
        private readonly IGroupMemberRepository _memberRepository;
        private readonly IStudentGroupRepository _groupRepository;
        private readonly IUserRepository _userRepository;
        // TODO: Add project, requirement, task, srs repositories when available

        public TeamLeaderService(
            IGroupMemberRepository memberRepository,
            IStudentGroupRepository groupRepository,
            IUserRepository userRepository)
        {
            _memberRepository = memberRepository;
            _groupRepository = groupRepository;
            _userRepository = userRepository;
        }

        /// <summary>
        /// BR-055: Validates that user is the leader of the group
        /// Throws exception if not the leader
        /// </summary>
        private async System.Threading.Tasks.Task ValidateLeaderAccessAsync(int userId, int groupId)
        {
            var groupMember = await _memberRepository.GetByGroupAndUserIdAsync(groupId, userId);
            
            if (groupMember == null || !groupMember.IsLeader.GetValueOrDefault(false))
            {
                throw new Exception("Access denied. You are not the leader of this group.");
            }
        }

        /// <summary>
        /// BR-055: Get project details for the leader's group
        /// Validates that user is leader of the group
        /// </summary>
        public async Task<ProjectResponseDTO?> GetGroupProjectAsync(int userId, int groupId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
            {
                throw new Exception("Group not found");
            }

            // BR-055: Validate user is leader of the group
            await ValidateLeaderAccessAsync(userId, groupId);

            // TODO: Get project from repository
            // For now, return null placeholder
            return null;
        }

        /// <summary>
        /// BR-055: Get all requirements for the leader's group project
        /// Validates that user is leader of the group
        /// </summary>
        public async Task<List<RequirementResponseDTO>> GetGroupRequirementsAsync(int userId, int groupId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
            {
                throw new Exception("Group not found");
            }

            // BR-055: Validate user is leader of the group
            await ValidateLeaderAccessAsync(userId, groupId);

            // TODO: Get requirements from repository
            return new List<RequirementResponseDTO>();
        }

        /// <summary>
        /// BR-055: Create a requirement for the leader's group
        /// Validates that user is leader of the group
        /// </summary>
        public async Task<RequirementResponseDTO> CreateRequirementAsync(int userId, int groupId, CreateRequirementDTO dto)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
            {
                throw new Exception("Group not found");
            }

            // BR-055: Validate user is leader of the group
            await ValidateLeaderAccessAsync(userId, groupId);

            // TODO: Create requirement in repository
            throw new NotImplementedException("Requirement repository needed");
        }

        /// <summary>
        /// BR-055: Update a requirement for the leader's group
        /// Validates that user is leader of the group
        /// </summary>
        public async Task<RequirementResponseDTO> UpdateRequirementAsync(int userId, int groupId, int requirementId, UpdateRequirementDTO dto)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
            {
                throw new Exception("Group not found");
            }

            // BR-055: Validate user is leader of the group
            await ValidateLeaderAccessAsync(userId, groupId);

            // TODO: Update requirement in repository
            throw new NotImplementedException("Requirement repository needed");
        }

        /// <summary>
        /// BR-055: Delete a requirement for the leader's group
        /// Validates that user is leader of the group
        /// </summary>
        public async System.Threading.Tasks.Task DeleteRequirementAsync(int userId, int groupId, int requirementId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
            {
                throw new Exception("Group not found");
            }

            // BR-055: Validate user is leader of the group
            await ValidateLeaderAccessAsync(userId, groupId);

            // TODO: Delete requirement in repository
            throw new NotImplementedException("Requirement repository needed");
        }

        /// <summary>
        /// BR-055: Get all tasks for the leader's group
        /// Validates that user is leader of the group
        /// </summary>
        public async Task<List<TaskResponseDTO>> GetGroupTasksAsync(int userId, int groupId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
            {
                throw new Exception("Group not found");
            }

            // BR-055: Validate user is leader of the group
            await ValidateLeaderAccessAsync(userId, groupId);

            // TODO: Get tasks from repository
            return new List<TaskResponseDTO>();
        }

        /// <summary>
        /// BR-055: Create a task for the leader's group
        /// Validates that user is leader of the group
        /// </summary>
        public async Task<TaskResponseDTO> CreateTaskAsync(int userId, int groupId, CreateTaskDTO dto)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
            {
                throw new Exception("Group not found");
            }

            // BR-055: Validate user is leader of the group
            await ValidateLeaderAccessAsync(userId, groupId);

            // TODO: Create task in repository
            throw new NotImplementedException("Task repository needed");
        }

        /// <summary>
        /// BR-055: Update a task for the leader's group
        /// Validates that user is leader of the group
        /// </summary>
        public async Task<TaskResponseDTO> UpdateTaskAsync(int userId, int groupId, int taskId, UpdateTaskDTO dto)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
            {
                throw new Exception("Group not found");
            }

            // BR-055: Validate user is leader of the group
            await ValidateLeaderAccessAsync(userId, groupId);

            // TODO: Update task in repository
            throw new NotImplementedException("Task repository needed");
        }

        /// <summary>
        /// BR-055: Assign task to team member
        /// Validates that user is leader of the group
        /// </summary>
        public async System.Threading.Tasks.Task AssignTaskAsync(int userId, int groupId, int taskId, int memberId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
            {
                throw new Exception("Group not found");
            }

            // BR-055: Validate user is leader of the group
            await ValidateLeaderAccessAsync(userId, groupId);

            // Verify member is in the group
            if (!await _memberRepository.IsMemberOfGroupAsync(groupId, memberId))
            {
                throw new Exception("Member is not part of this group");
            }

            // TODO: Assign task in repository
            throw new NotImplementedException("Task repository needed");
        }

        /// <summary>
        /// BR-055: Get SRS document for the leader's group
        /// Validates that user is leader of the group
        /// </summary>
        public async Task<SrsDocumentResponseDTO?> GetGroupSrsDocumentAsync(int userId, int groupId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
            {
                throw new Exception("Group not found");
            }

            // BR-055: Validate user is leader of the group
            await ValidateLeaderAccessAsync(userId, groupId);

            // TODO: Get SRS document from repository
            return null;
        }

        /// <summary>
        /// BR-055: Create SRS document for the leader's group
        /// Validates that user is leader of the group
        /// </summary>
        public async Task<SrsDocumentResponseDTO> CreateSrsDocumentAsync(int userId, int groupId, CreateSrsDocumentDTO dto)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
            {
                throw new Exception("Group not found");
            }

            // BR-055: Validate user is leader of the group
            await ValidateLeaderAccessAsync(userId, groupId);

            // TODO: Create SRS document in repository
            throw new NotImplementedException("SRS repository needed");
        }

        /// <summary>
        /// BR-055: Update SRS document for the leader's group
        /// Validates that user is leader of the group
        /// </summary>
        public async Task<SrsDocumentResponseDTO> UpdateSrsDocumentAsync(int userId, int groupId, int srsId, UpdateSrsDocumentDTO dto)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
            {
                throw new Exception("Group not found");
            }

            // BR-055: Validate user is leader of the group
            await ValidateLeaderAccessAsync(userId, groupId);

            // TODO: Update SRS document in repository
            throw new NotImplementedException("SRS repository needed");
        }
    }
}
