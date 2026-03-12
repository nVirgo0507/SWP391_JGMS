using System.ComponentModel.DataAnnotations;
using DAL.Models;

namespace BLL.DTOs.Admin
{
    /// <summary>
    /// BR-054: Request DTO to update student group details
    /// Lecturers can update group information and status
    /// </summary>
    public class UpdateStudentGroupDTO
    {
        [RegularExpression(@"^SE\d+$", ErrorMessage = "Group code must start with 'SE' followed by numbers (e.g. SE1234)")]
        public string? GroupCode { get; set; }
        public string? GroupName { get; set; }

        /// <summary>
        /// Lecturer identifier — accepts either a numeric user ID or an email address. Optional.
        /// </summary>
        public string? LecturerId { get; set; }

        /// <summary>
        /// Leader identifier — accepts either a numeric user ID or an email address. Optional.
        /// </summary>
        public string? LeaderId { get; set; }

        public UserStatus? Status { get; set; }
    }
}
