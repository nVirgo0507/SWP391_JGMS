﻿using System;
using System.Collections.Generic;

namespace DAL.Models;

/// <summary>
/// Lecturer: manage students in assigned groups
/// </summary>
public partial class GroupMember
{
    public int MembershipId { get; set; }

    public int GroupId { get; set; }

    public int UserId { get; set; }

    public bool? IsLeader { get; set; }

    public DateTime? JoinedAt { get; set; }

    public DateTime? LeftAt { get; set; }

    public virtual StudentGroup Group { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
