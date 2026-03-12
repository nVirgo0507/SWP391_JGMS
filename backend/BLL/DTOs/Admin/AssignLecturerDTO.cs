using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs.Admin
{
    /// <summary>
    /// BR-054: Request DTO to assign a lecturer to a student group
    /// BR-054: Lecturer will have group-scoped access to the assigned group
    /// </summary>
    public class AssignLecturerDTO
    {
        /// <summary>
        /// Lecturer identifier — accepts either a numeric user ID (e.g. "5") or an email address (e.g. "lecturer@fpt.edu.vn").
        /// </summary>
        [Required]
        public string LecturerId { get; set; } = null!;
    }
}
