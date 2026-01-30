using BLL.DTOs.Admin;

namespace BLL.Services.Interface
{
    public interface IAdminService
    {
        // User Management
        Task<UserResponseDTO> CreateUserAsync(CreateUserDTO dto);
        Task<UserResponseDTO> UpdateUserAsync(int userId, UpdateUserDTO dto);
        Task<UserResponseDTO?> GetUserByIdAsync(int userId);
        Task<List<UserResponseDTO>> GetAllUsersAsync();
        Task<List<UserResponseDTO>> GetUsersByRoleAsync(string role);
        System.Threading.Tasks.Task DeleteUserAsync(int userId);
        System.Threading.Tasks.Task SetUserStatusAsync(int userId, string status);

        // Student Group Management
        Task<StudentGroupResponseDTO> CreateStudentGroupAsync(CreateStudentGroupDTO dto);
        Task<StudentGroupResponseDTO> UpdateStudentGroupAsync(int groupId, UpdateStudentGroupDTO dto);
        Task<StudentGroupResponseDTO?> GetStudentGroupByIdAsync(int groupId);
        Task<List<StudentGroupResponseDTO>> GetAllStudentGroupsAsync();
        System.Threading.Tasks.Task DeleteStudentGroupAsync(int groupId);

        // Lecturer Management
        Task<List<UserResponseDTO>> GetAllLecturersAsync();
        System.Threading.Tasks.Task AssignLecturerToGroupAsync(int groupId, int lecturerId);

        // Group Member Management
        System.Threading.Tasks.Task AddStudentToGroupAsync(int groupId, int studentId);
        System.Threading.Tasks.Task RemoveStudentFromGroupAsync(int groupId, int studentId);
    }
}
