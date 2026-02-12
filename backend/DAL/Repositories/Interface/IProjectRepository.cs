using DAL.Models;

namespace DAL.Repositories.Interface
{
    public interface IProjectRepository
    {
        System.Threading.Tasks.Task<Project?> GetByIdAsync(int projectId);
        System.Threading.Tasks.Task<Project?> GetByGroupIdAsync(int groupId);
        System.Threading.Tasks.Task<List<Project>> GetAllAsync();
        System.Threading.Tasks.Task AddAsync(Project project);
        System.Threading.Tasks.Task UpdateAsync(Project project);
        System.Threading.Tasks.Task DeleteAsync(int projectId);
    }
}


