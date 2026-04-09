using AdminDTOs = BLL.DTOs.Admin;

namespace BLL.DTOs.Student
{
    /// <summary>
    /// Returns the group a student currently belongs to,
    /// along with basic project and team-member information.
    /// </summary>
    public class MyGroupDTO
    {
        public int GroupId { get; set; }
        public string GroupCode { get; set; } = null!;
        public string GroupName { get; set; } = null!;
        public bool IsLeader { get; set; }
        public DateTime? JoinedAt { get; set; }

        public string LecturerName { get; set; } = null!;

        public int? ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public AdminDTOs.ProjectIntegrationStatusDTO? JiraStatus { get; set; }
        public AdminDTOs.ProjectIntegrationStatusDTO? GithubStatus { get; set; }

        public List<MyGroupMemberDTO> Members { get; set; } = new();
    }

    public class MyGroupMemberDTO
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public bool IsLeader { get; set; }
        public DateTime? JoinedAt { get; set; }
    }
}

