using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs.Admin
{
    /// <summary>
    /// BR-007: Request DTO to set user status (active/inactive)
    /// BR-007: Admins can set user status to control login access
    /// </summary>
    public class SetUserStatusDTO
    {
        [Required]
        public string Status { get; set; } = null!;
    }
}
