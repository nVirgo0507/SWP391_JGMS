﻿﻿using DAL.Models;
using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs.Admin
{
    public class CreateUserDTO : IValidatableObject
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;

        [Required]
        [MinLength(8)]
        public string Password { get; set; } = null!;

        [Required]
        public string FullName { get; set; } = null!;

        [Required]
        public UserRole Role { get; set; }

        // Student-specific fields (REQUIRED for students)
        public string? StudentCode { get; set; }

        /// <summary>
        /// GitHub username (REQUIRED for students).
        /// Must be alphanumeric with hyphens, max 39 characters, cannot start/end with hyphen.
        /// BR-004: GitHub Username Format validation
        /// </summary>
        [RegularExpression(@"^[a-zA-Z0-9]([a-zA-Z0-9-]{0,37}[a-zA-Z0-9]|[a-zA-Z0-9])?$",
            ErrorMessage = "GitHub username must be alphanumeric with hyphens, 1-39 characters, and cannot start or end with a hyphen")]
        public string? GithubUsername { get; set; }

        /// <summary>
        /// Jira Account ID (REQUIRED for students)
        /// </summary>
        public string? JiraAccountId { get; set; }

        // Phone is required for all roles
        [Required]
        public string Phone { get; set; } = null!;

        public UserStatus Status { get; set; } = UserStatus.active;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // Student-specific validation
            if (Role == UserRole.student)
            {
                if (string.IsNullOrWhiteSpace(StudentCode))
                {
                    yield return new ValidationResult(
                        "Student code is required for students",
                        new[] { nameof(StudentCode) });
                }

                if (string.IsNullOrWhiteSpace(GithubUsername))
                {
                    yield return new ValidationResult(
                        "GitHub username is required for students",
                        new[] { nameof(GithubUsername) });
                }

                if (string.IsNullOrWhiteSpace(JiraAccountId))
                {
                    yield return new ValidationResult(
                        "Jira account ID is required for students",
                        new[] { nameof(JiraAccountId) });
                }
            }
        }
    }
}
