using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs.Admin
{
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
