﻿using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs.Student
{
    /// <summary>
    /// DTO for students to update their basic profile information
    /// </summary>
    public class UpdateProfileDTO
    {
        /// <summary>
        /// Phone number
        /// </summary>
        [Phone]
        public string? Phone { get; set; }

        /// <summary>
        /// GitHub username.
        /// Must be alphanumeric with hyphens, max 39 characters, cannot start/end with hyphen.
        /// BR-004: GitHub Username Format validation
        /// </summary>
        [RegularExpression(@"^[a-zA-Z0-9]([a-zA-Z0-9-]{0,37}[a-zA-Z0-9])?$",
            ErrorMessage = "GitHub username must be alphanumeric with hyphens, 1-39 characters, and cannot start or end with a hyphen")]
        public string? GithubUsername { get; set; }

        /// <summary>
        /// Jira account ID
        /// </summary>
        public string? JiraAccountId { get; set; }
    }
}
