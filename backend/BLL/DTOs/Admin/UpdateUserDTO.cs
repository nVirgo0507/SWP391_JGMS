﻿﻿using DAL.Models;
using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs.Admin
{
    public class UpdateUserDTO
    {
        [EmailAddress]
        public string? Email { get; set; }

        public string? FullName { get; set; }

        public UserRole? Role { get; set; }

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

        // Phone is required for all roles (but optional in updates)
        public string? Phone { get; set; }

        public UserStatus? Status { get; set; }
    }
}

