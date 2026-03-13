﻿using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs.Admin
{
    /// <summary>
    /// Request DTO to create a new student group.
    /// Requires valid lecturer ID. Leader and initial members are optional.
    /// If a leader is specified, they are automatically added as a group member.
    /// </summary>
    public class CreateStudentGroupDTO
    {
        [Required]
        [RegularExpression(@"^SE\d+$", ErrorMessage = "Group code must start with 'SE' followed by numbers (e.g. SE1234)")]
        public string GroupCode { get; set; } = null!;

        [Required]
        public string GroupName { get; set; } = null!;

        /// <summary>
        /// Lecturer identifier — accepts either a numeric user ID (e.g. "5") or an email address (e.g. "lecturer@fpt.edu.vn").
        /// </summary>
        [Required]
        public string LecturerId { get; set; } = null!;

        /// <summary>
        /// Leader identifier — accepts either a numeric user ID or an email address. Optional.
        /// </summary>
        public string? LeaderId { get; set; }

        /// <summary>
        /// Optional list of student identifiers (numeric ID or email) for initial group members.
        /// The leader (if specified) is added automatically — no need to include them here.
        /// </summary>
        public List<string>? MemberIds { get; set; }
    }
}
