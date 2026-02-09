using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs.Admin
{
    /// <summary>
    /// BR-053: Request DTO to create a new student group
    /// Requires valid lecturer and team leader user IDs
    /// </summary>
    public class CreateStudentGroupDTO
    {
        [Required]
        public string GroupCode { get; set; } = null!;

        [Required]
        public string GroupName { get; set; } = null!;

        [Required]
        public int LecturerId { get; set; }

        public int? LeaderId { get; set; }
    }
}
