using DAL.Repositories.Interface;

namespace BLL.Helpers
{
    /// <summary>
    /// Resolves human-readable identifiers (group codes, emails, requirement codes)
    /// to internal database IDs. Used by controllers so the API accepts friendly names
    /// instead of auto-generated IDs.
    /// </summary>
    public class IdentifierResolver
    {
        private readonly IStudentGroupRepository _groupRepo;
        private readonly IUserRepository _userRepo;
        private readonly IProjectRepository _projectRepo;
        private readonly IRequirementRepository _requirementRepo;

        public IdentifierResolver(
            IStudentGroupRepository groupRepo,
            IUserRepository userRepo,
            IProjectRepository projectRepo,
            IRequirementRepository requirementRepo)
        {
            _groupRepo = groupRepo;
            _userRepo = userRepo;
            _projectRepo = projectRepo;
            _requirementRepo = requirementRepo;
        }

        /// <summary>
        /// Resolve a group identifier — accepts either a numeric ID or a group code (e.g. "SE1234").
        /// </summary>
        public async Task<int> ResolveGroupIdAsync(string groupIdentifier)
        {
            // If it's a numeric ID, use directly
            if (int.TryParse(groupIdentifier, out var groupId))
            {
                var groupById = await _groupRepo.GetByIdAsync(groupId);
                if (groupById != null) return groupId;
            }

            // Otherwise treat as group code
            var group = await _groupRepo.GetByGroupCodeAsync(groupIdentifier);
            if (group == null)
                throw new KeyNotFoundException($"Group '{groupIdentifier}' not found. Provide a valid group code (e.g. 'SE1234') or group ID.");
            return group.GroupId;
        }

        /// <summary>
        /// Resolve a user identifier — accepts either a numeric ID or an email address.
        /// </summary>
        public async Task<int> ResolveUserIdAsync(string userIdentifier)
        {
            if (int.TryParse(userIdentifier, out var userId))
            {
                var userById = await _userRepo.GetByIdAsync(userId);
                if (userById != null) return userId;
            }

            var user = await _userRepo.GetByEmailAsync(userIdentifier);
            if (user == null)
                throw new KeyNotFoundException($"User '{userIdentifier}' not found. Provide a valid email or user ID.");
            return user.UserId;
        }

        /// <summary>
        /// Resolve a project identifier — accepts a numeric project ID, a numeric group ID, or a group code.
        /// Since each group has exactly one project, group code is the most user-friendly option.
        /// </summary>
        public async Task<int> ResolveProjectIdAsync(string projectIdentifier)
        {
            // Try as project ID first
            if (int.TryParse(projectIdentifier, out var projectId))
            {
                var projectById = await _projectRepo.GetByIdAsync(projectId);
                if (projectById != null) return projectId;

                // Maybe it's a group ID
                var projectByGroupId = await _projectRepo.GetByGroupIdAsync(projectId);
                if (projectByGroupId != null) return projectByGroupId.ProjectId;
            }

            // Otherwise treat as group code → resolve to project
            var group = await _groupRepo.GetByGroupCodeAsync(projectIdentifier);
            if (group == null)
                throw new KeyNotFoundException($"Project '{projectIdentifier}' not found. Provide a valid group code (e.g. 'SE1234'), project ID, or group ID.");

            var project = await _projectRepo.GetByGroupIdAsync(group.GroupId);
            if (project == null)
                throw new KeyNotFoundException($"No project found for group '{projectIdentifier}'.");
            return project.ProjectId;
        }

        /// <summary>
        /// Resolve a requirement identifier — accepts either a numeric ID or a requirement code (e.g. "REQ-001").
        /// Requires projectId to scope the code lookup.
        /// </summary>
        public async Task<int> ResolveRequirementIdAsync(string requirementIdentifier, int projectId)
        {
            if (int.TryParse(requirementIdentifier, out var reqId))
            {
                var reqById = await _requirementRepo.GetByIdAsync(reqId);
                if (reqById != null) return reqId;
            }

            // Search by requirement code within the project
            var requirements = await _requirementRepo.GetByProjectIdAsync(projectId);
            var req = requirements.FirstOrDefault(r =>
                r.RequirementCode.Equals(requirementIdentifier, StringComparison.OrdinalIgnoreCase));
            if (req == null)
                throw new KeyNotFoundException($"Requirement '{requirementIdentifier}' not found. Provide a valid requirement code (e.g. 'REQ-001') or requirement ID.");
            return req.RequirementId;
        }
    }
}

