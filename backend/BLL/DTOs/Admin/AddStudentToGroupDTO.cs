using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs.Admin
{
    /// <summary>
    /// Request DTO to add one or more students to a group in a single call.
    /// Each identifier can be an email address or a numeric user ID.
    /// </summary>
    public class AddStudentsToGroupDTO
    {
        /// <summary>
        /// Array of student identifiers (email or numeric ID).
        /// Example: ["student1@fpt.edu.vn", "42", "student3@fpt.edu.vn"]
        /// </summary>
        [Required]
        [MinLength(1, ErrorMessage = "Provide at least one student identifier.")]
        public List<string> StudentIdentifiers { get; set; } = new();
    }
}
