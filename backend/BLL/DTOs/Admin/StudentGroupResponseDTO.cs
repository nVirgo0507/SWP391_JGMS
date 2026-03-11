using DAL.Models;

namespace BLL.DTOs.Admin
{
    public class StudentGroupResponseDTO
    {
        public int GroupId { get; set; }
        public string GroupCode { get; set; } = null!;
        public string GroupName { get; set; } = null!;
        public int LecturerId { get; set; }
        public string LecturerName { get; set; } = null!;
        public int? LeaderId { get; set; }
        public string? LeaderName { get; set; }
        public UserStatus? Status { get; set; }
        public int MemberCount { get; set; }
        /// <summary>Full list of current (active) members, ordered by leader first then name.</summary>
        public List<GroupMemberResponseDTO> Members { get; set; } = new();
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
