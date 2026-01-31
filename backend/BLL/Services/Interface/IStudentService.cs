using BLL.DTOs.Student;
using BLL.DTOs.Admin;

namespace BLL.Services.Interface
{
    public interface IStudentService
    {
        // View Assigned Tasks
        System.Threading.Tasks.Task<List<TaskResponseDTO>> GetMyTasksAsync(int userId);
        System.Threading.Tasks.Task<TaskResponseDTO?> GetTaskByIdAsync(int taskId, int userId);
        
        // Update Task Status
        System.Threading.Tasks.Task UpdateTaskStatusAsync(int taskId, int userId, UpdateTaskStatusDTO dto);
        
        // View Personal Statistics
        System.Threading.Tasks.Task<PersonalStatisticsDTO> GetPersonalStatisticsAsync(int userId, int? projectId = null);
        System.Threading.Tasks.Task<List<CommitHistoryDTO>> GetCommitHistoryAsync(int userId, int? projectId = null);
        
        // Profile Management
        System.Threading.Tasks.Task<UserResponseDTO> GetMyProfileAsync(int userId);
        System.Threading.Tasks.Task<UserResponseDTO> UpdateMyProfileAsync(int userId, UpdateProfileDTO dto);
        
        // SRS Document
        System.Threading.Tasks.Task<List<SrsDocumentDTO>> GetSrsDocumentsByProjectAsync(int projectId, int userId);
        System.Threading.Tasks.Task<SrsDocumentDTO?> GetSrsDocumentByIdAsync(int documentId, int userId);
    }
}
