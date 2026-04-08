using DAL.Models;
using DAL.Repositories.Interface;
using Microsoft.EntityFrameworkCore;
using Task = System.Threading.Tasks.Task;

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

        public async Task<List<Commit>> GetCommitsByProjectIdAsync(int projectId)
        {
            return await _context.Commits
                .Include(c => c.User)
                .Include(c => c.Project)
                .Include(c => c.GithubCommit)
                .Where(c => c.ProjectId == projectId)
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

        public async Task<bool> ExistsByGithubCommitIdAsync(int githubCommitId)
        {
            return await _context.Commits.AnyAsync(c => c.GithubCommitId == githubCommitId);
        }

        public async Task<List<Commit>> GetCommitsByProjectIdAsync(int projectId)
        {
            return await _context.Commits
                .Include(c => c.Project)
                .Include(c => c.GithubCommit)
                .Where(c => c.ProjectId == projectId)
                .OrderByDescending(c => c.CommitDate)
                .ToListAsync();
        }

        public async System.Threading.Tasks.Task AddAsync(Commit commit)
        {
            await _context.Commits.AddAsync(commit);
            await _context.SaveChangesAsync();
        }

        public async System.Threading.Tasks.Task AddRangeAsync(IEnumerable<Commit> commits)
        {
            await _context.Commits.AddRangeAsync(commits);
            await _context.SaveChangesAsync();
        }
    }
}
