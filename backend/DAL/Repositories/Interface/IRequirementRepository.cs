using DAL.Models;

namespace DAL.Repositories.Interface
{
    /// <summary>
    /// Repository for managing Requirements
    /// </summary>
    public interface IRequirementRepository
    {
        System.Threading.Tasks.Task<List<Requirement>> GetByProjectIdAsync(int projectId);
        System.Threading.Tasks.Task<Requirement?> GetByIdAsync(int requirementId);
        System.Threading.Tasks.Task<Requirement?> GetByJiraIssueIdAsync(int jiraIssueId);
        System.Threading.Tasks.Task<bool> ExistsByCodeAsync(int projectId, string code, int? excludeId = null);
        System.Threading.Tasks.Task AddAsync(Requirement requirement);
        System.Threading.Tasks.Task UpdateAsync(Requirement requirement);
        System.Threading.Tasks.Task DeleteAsync(int requirementId);
    }
}


