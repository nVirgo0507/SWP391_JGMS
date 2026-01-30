using DAL.Models;

namespace BLL.DTOs.Admin
{
    public class UpdateStudentGroupDTO
    {
        public string? GroupCode { get; set; }
        public string? GroupName { get; set; }
        public int? LecturerId { get; set; }
        public int? LeaderId { get; set; }
        public UserStatus? Status { get; set; }
    }
}
