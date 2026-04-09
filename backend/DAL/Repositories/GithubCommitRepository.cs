using DAL.Models;
using DAL.Repositories.Interface;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DAL.Repositories
{
    public class GithubCommitRepository : IGithubCommitRepository
    {
        private readonly JgmsContext _context;

        public GithubCommitRepository(JgmsContext context)
        {
            _context = context;
        }

        public async Task<List<GithubCommit>> GetCommitsByProjectIdAsync(int projectId)
        {
            return await _context.GithubCommits
                .Where(c => c.ProjectId == projectId)
                .OrderByDescending(c => c.CommitDate)
                .ToListAsync();
        }

        public async Task<GithubCommit?> GetByShaAsync(string sha)
        {
            return await _context.GithubCommits
                .FirstOrDefaultAsync(c => c.CommitSha == sha);
        }

        public async Task<bool> CommitExistsAsync(string sha)
        {
            return await _context.GithubCommits
                .AnyAsync(c => c.CommitSha == sha);
        }

        public async System.Threading.Tasks.Task AddAsync(GithubCommit commit)
        {
            await _context.GithubCommits.AddAsync(commit);
            await _context.SaveChangesAsync();
        }

        public async System.Threading.Tasks.Task AddRangeAsync(IEnumerable<GithubCommit> commits)
        {
            await _context.GithubCommits.AddRangeAsync(commits);
            await _context.SaveChangesAsync();
        }

        public async Task<int> GetCountByProjectIdAsync(int projectId)
        {
            return await _context.GithubCommits.CountAsync(c => c.ProjectId == projectId);
        }
    }
}
