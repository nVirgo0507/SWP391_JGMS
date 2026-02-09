using DAL.Models;

namespace BLL.DTOs.Admin
{
    /// <summary>
    /// BR-054: Request DTO to update student group details
    /// Lecturers can update group information and status
    /// </summary>
    public class UpdateStudentGroupDTO
    {
        public string? GroupCode { get; set; }
        public string? GroupName { get; set; }
        public int? LecturerId { get; set; }
        public int? LeaderId { get; set; }
        public UserStatus? Status { get; set; }
    }
}
