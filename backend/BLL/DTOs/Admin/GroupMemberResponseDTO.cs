using System;
using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs.Admin
{
    /// <summary>
    /// BR-054: Response DTO for group member details
    /// Used when lecturer retrieves members of their assigned groups
    /// </summary>
    public class GroupMemberResponseDTO
    {
        [Required]
        public int MemberId { get; set; }

        [Required]
        public int GroupId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public string UserName { get; set; } = null!;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;

        public bool IsLeader { get; set; }

        public DateTime? JoinedAt { get; set; }
    }
}
