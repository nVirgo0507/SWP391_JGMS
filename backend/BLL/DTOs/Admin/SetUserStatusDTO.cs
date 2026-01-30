using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs.Admin
{
    public class SetUserStatusDTO
    {
        [Required]
        public string Status { get; set; } = null!;
    }
}
