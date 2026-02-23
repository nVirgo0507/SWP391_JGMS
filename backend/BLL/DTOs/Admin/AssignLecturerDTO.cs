using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs.Admin
{
    /// <summary>
    /// BR-054: Request DTO to assign a lecturer to a student group
    /// BR-054: Lecturer will have group-scoped access to the assigned group
    /// </summary>
    public class AssignLecturerDTO
    {
        [Required]
        public int LecturerId { get; set; }
    }
}
