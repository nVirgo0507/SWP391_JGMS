using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs.Admin
{
    /// <summary>
    /// BR-055: Request DTO to assign a task to a team member
    /// Used by team leader to assign tasks to their group members
    /// </summary>
    public class AssignTaskDTO
    {
        [Required]
        public int MemberId { get; set; }
    }
}
