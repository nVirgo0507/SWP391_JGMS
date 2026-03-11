using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs
{
    /// <summary>
    /// Request DTO for lecturer self-registration.
    /// Lecturers do not require a student code, GitHub username, or Jira account ID.
    /// </summary>
    public class RegisterLecturerDTO
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;

        /// <summary>
        /// Must be at least 8 characters with uppercase, lowercase, and a digit.
        /// </summary>
        [Required]
        [MinLength(8)]
        public string Password { get; set; } = null!;

        [Required]
        public string FullName { get; set; } = null!;

        /// <summary>
        /// Vietnamese phone number. Accepts 0XXXXXXXXX or +84XXXXXXXXX format.
        /// </summary>
        [Required]
        public string Phone { get; set; } = null!;
    }
}

