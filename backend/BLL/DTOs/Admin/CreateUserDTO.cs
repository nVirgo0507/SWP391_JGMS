﻿using DAL.Models;
using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs.Admin
{
    public class CreateUserDTO
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

        // Student-specific fields
        public string? StudentCode { get; set; }

        /// <summary>
        /// GitHub username.
        /// Must be alphanumeric with hyphens, max 39 characters, cannot start/end with hyphen.
        /// BR-004: GitHub Username Format validation
        /// </summary>
        [RegularExpression(@"^[a-zA-Z0-9]([a-zA-Z0-9-]{0,37}[a-zA-Z0-9]|[a-zA-Z0-9])?$",
            ErrorMessage = "GitHub username must be alphanumeric with hyphens, 1-39 characters, and cannot start or end with a hyphen")]
        public string? GithubUsername { get; set; }

        public string? JiraAccountId { get; set; }

        // Lecturer-specific fields
        public string? Phone { get; set; }

        public UserStatus Status { get; set; } = UserStatus.active;
    }
}
