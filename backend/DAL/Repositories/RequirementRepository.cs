using DAL.Models;
using DAL.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repositories
{
    public class RequirementRepository : IRequirementRepository
    {
        private readonly JgmsContext _context;

        public RequirementRepository(JgmsContext context)
        {
            _context = context;
        }

        public async System.Threading.Tasks.Task<List<Requirement>> GetByProjectIdAsync(int projectId)
        {
            return await _context.Requirements
                .Include(r => r.CreatedByNavigation)
                .Include(r => r.JiraIssue)
                .Where(r => r.ProjectId == projectId)
                .OrderBy(r => r.RequirementCode)
                .ToListAsync();
        }

        public async System.Threading.Tasks.Task<Requirement?> GetByIdAsync(int requirementId)
        {
            return await _context.Requirements
                .Include(r => r.CreatedByNavigation)
                .Include(r => r.JiraIssue)
                .FirstOrDefaultAsync(r => r.RequirementId == requirementId);
        }

        public async System.Threading.Tasks.Task<Requirement?> GetByJiraIssueIdAsync(int jiraIssueId)
        {
            return await _context.Requirements
                .FirstOrDefaultAsync(r => r.JiraIssueId == jiraIssueId);
        }

        public async System.Threading.Tasks.Task<bool> ExistsByCodeAsync(int projectId, string code, int? excludeId = null)
        {
            return await _context.Requirements
                .AnyAsync(r => r.ProjectId == projectId
                               && r.RequirementCode == code
                               && (excludeId == null || r.RequirementId != excludeId));
        }

        public async System.Threading.Tasks.Task AddAsync(Requirement requirement)
        {
            requirement.CreatedAt = DateTime.UtcNow;
            requirement.UpdatedAt = DateTime.UtcNow;
            await _context.Requirements.AddAsync(requirement);
            await _context.SaveChangesAsync();
        }

        public async System.Threading.Tasks.Task UpdateAsync(Requirement requirement)
        {
            requirement.UpdatedAt = DateTime.UtcNow;
            _context.Requirements.Update(requirement);
            await _context.SaveChangesAsync();
        }

        public async System.Threading.Tasks.Task DeleteAsync(int requirementId)
        {
            var requirement = await GetByIdAsync(requirementId);
            if (requirement != null)
            {
                _context.Requirements.Remove(requirement);
                await _context.SaveChangesAsync();
            }
        }
    }
}



