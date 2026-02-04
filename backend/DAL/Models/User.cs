﻿using System;
using System.Collections.Generic;

namespace DAL.Models;

/// <summary>
/// All users: Admin, Lecturer, Student
/// </summary>
public partial class User
{
    public int UserId { get; set; }

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string? StudentCode { get; set; }

    public string? GithubUsername { get; set; }

    public string? JiraAccountId { get; set; }

    public string Phone { get; set; } = null!;

	public UserRole Role { get; set; }

	public UserStatus? Status { get; set; } = UserStatus.active;

	public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<CommitStatistic> CommitStatistics { get; set; } = new List<CommitStatistic>();

    public virtual ICollection<Commit> Commits { get; set; } = new List<Commit>();

    public virtual ICollection<GroupMember> GroupMembers { get; set; } = new List<GroupMember>();

    public virtual ICollection<PersonalTaskStatistic> PersonalTaskStatistics { get; set; } = new List<PersonalTaskStatistic>();

    public virtual ICollection<ProgressReport> ProgressReports { get; set; } = new List<ProgressReport>();

    public virtual ICollection<Requirement> Requirements { get; set; } = new List<Requirement>();

    public virtual ICollection<SrsDocument> SrsDocuments { get; set; } = new List<SrsDocument>();

    public virtual ICollection<StudentGroup> StudentGroupLeaders { get; set; } = new List<StudentGroup>();

    public virtual ICollection<StudentGroup> StudentGroupLecturers { get; set; } = new List<StudentGroup>();

    public virtual ICollection<Task> Tasks { get; set; } = new List<Task>();
}
