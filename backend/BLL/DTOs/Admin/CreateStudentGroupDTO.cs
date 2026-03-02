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

        [Required]
        public int LecturerId { get; set; }

        public int? LeaderId { get; set; }

        /// <summary>
        /// Optional list of student user IDs to add as initial group members.
        /// The leader (if specified) is added automatically — no need to include them here.
        /// </summary>
        public List<int>? MemberIds { get; set; }
    }
}
