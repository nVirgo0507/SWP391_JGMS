using DAL.Models;
using DAL.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repositories
{
    public class CommitRepository : ICommitRepository
    {
        private readonly JgmsContext _context;

        public CommitRepository(JgmsContext context)
        {
            _context = context;
        }

        public async Task<List<Commit>> GetCommitsByUserIdAsync(int userId)
        {
            return await _context.Commits
                .Include(c => c.Project)
                .Include(c => c.GithubCommit)
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.CommitDate)
                .ToListAsync();
        }

        public async Task<List<Commit>> GetCommitsByUserIdAndProjectIdAsync(int userId, int projectId)
        {
            return await _context.Commits
                .Include(c => c.Project)
                .Include(c => c.GithubCommit)
                .Where(c => c.UserId == userId && c.ProjectId == projectId)
                .OrderByDescending(c => c.CommitDate)
                .ToListAsync();
        }

        public async Task<Commit?> GetByIdAsync(int commitId)
        {
            return await _context.Commits
                .Include(c => c.Project)
                .Include(c => c.GithubCommit)
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.CommitId == commitId);
        }
    }
}
