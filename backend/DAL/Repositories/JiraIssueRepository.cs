using DAL.Data;
using DAL.Models;
using DAL.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repositories
{
    /// <summary>
    /// Implementation of Jira Issue repository
    /// </summary>
    public class JiraIssueRepository : IJiraIssueRepository
    {
        private readonly JgmsContext _context;

        public JiraIssueRepository(JgmsContext context)
        {
            _context = context;
        }

        public async Task<List<JiraIssue>> GetByProjectIdAsync(int projectId)
        {
            return await _context.JiraIssues
                .Where(j => j.ProjectId == projectId)
                .OrderByDescending(j => j.UpdatedDate)
                .ToListAsync();
        }

        public async Task<JiraIssue?> GetByIssueKeyAsync(string issueKey)
        {
            return await _context.JiraIssues
                .FirstOrDefaultAsync(j => j.IssueKey == issueKey);
        }

        public async Task<JiraIssue?> GetByJiraIdAsync(string jiraId)
        {
            return await _context.JiraIssues
                .FirstOrDefaultAsync(j => j.JiraId == jiraId);
        }

        public async Task<JiraIssue?> GetByIdAsync(int jiraIssueId)
        {
            return await _context.JiraIssues
                .Include(j => j.Project)
                .FirstOrDefaultAsync(j => j.JiraIssueId == jiraIssueId);
        }

        public async System.Threading.Tasks.Task AddAsync(JiraIssue issue)
        {
            issue.CreatedAt = DateTime.UtcNow;
            issue.LastSynced = DateTime.UtcNow;
            await _context.JiraIssues.AddAsync(issue);
            await _context.SaveChangesAsync();
        }

        public async System.Threading.Tasks.Task AddRangeAsync(List<JiraIssue> issues)
        {
            foreach (var issue in issues)
            {
                issue.CreatedAt = DateTime.UtcNow;
                issue.LastSynced = DateTime.UtcNow;
            }
            await _context.JiraIssues.AddRangeAsync(issues);
            await _context.SaveChangesAsync();
        }

        public async System.Threading.Tasks.Task UpdateAsync(JiraIssue issue)
        {
            issue.LastSynced = DateTime.UtcNow;
            _context.JiraIssues.Update(issue);
            await _context.SaveChangesAsync();
        }

        public async Task<DateTime?> GetLastSyncTimeAsync(int projectId)
        {
            return await _context.JiraIssues
                .Where(j => j.ProjectId == projectId)
                .MaxAsync(j => (DateTime?)j.LastSynced);
        }

        public async Task<List<JiraIssue>> GetUnassignedIssuesAsync(int projectId)
        {
            return await _context.JiraIssues
                .Where(j => j.ProjectId == projectId && string.IsNullOrEmpty(j.AssigneeJiraId))
                .ToListAsync();
        }

        public async Task<List<JiraIssue>> GetByStatusAsync(int projectId, string status)
        {
            return await _context.JiraIssues
                .Where(j => j.ProjectId == projectId && j.Status == status)
                .ToListAsync();
        }
    }
}


