using DAL.Models;

namespace DAL.Repositories.Interface
{
    public interface ITaskRepository
    {
        System.Threading.Tasks.Task<List<DAL.Models.Task>> GetTasksByUserIdAsync(int userId);
        System.Threading.Tasks.Task<DAL.Models.Task?> GetByIdAsync(int taskId);
        System.Threading.Tasks.Task UpdateAsync(DAL.Models.Task task);
        System.Threading.Tasks.Task<List<DAL.Models.Task>> GetTasksByProjectIdAsync(int projectId);
        System.Threading.Tasks.Task<List<DAL.Models.Task>> GetOverdueTasksByUserIdAsync(int userId);
        System.Threading.Tasks.Task<int> CountTasksByStatusAsync(int userId, string status);
    }
}
