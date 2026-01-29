using System;
using System.Collections.Generic;

namespace DAL.Models;

/// <summary>
/// Admin: manage student groups, assign lecturers to groups
/// </summary>
public partial class StudentGroup
{
    public int GroupId { get; set; }

    public string GroupCode { get; set; } = null!;

    public string GroupName { get; set; } = null!;

    public int LecturerId { get; set; }

    public int? LeaderId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<GroupMember> GroupMembers { get; set; } = new List<GroupMember>();

    public virtual User? Leader { get; set; }

    public virtual User Lecturer { get; set; } = null!;

    public virtual Project? Project { get; set; }
}
