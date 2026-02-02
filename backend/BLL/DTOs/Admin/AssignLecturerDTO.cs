using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs.Admin
{
    public class AssignLecturerDTO
    {
        [Required]
        public int LecturerId { get; set; }
    }
}
