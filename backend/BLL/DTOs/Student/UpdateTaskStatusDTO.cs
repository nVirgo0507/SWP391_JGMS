using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs.Student
{
    /// <summary>
    /// DTO for updating task status
    /// </summary>
    public class UpdateTaskStatusDTO
    {
        [Required]
        public string Status { get; set; } = null!; // e.g., "To Do", "In Progress", "Done"
        
        public string? Comment { get; set; }
        
        public int? WorkHours { get; set; } // Hours spent on the task
    }
}
