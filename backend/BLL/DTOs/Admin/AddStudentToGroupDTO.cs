using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs.Admin
{
    /// <summary>
    /// BR-054: Request DTO to add a student to a group
    /// Used by lecturer to add students to their assigned groups
    /// </summary>
    public class AddStudentToGroupDTO
    {
        [Required]
        public int StudentId { get; set; }
    }
}
