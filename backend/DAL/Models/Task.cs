﻿using System;
using System.Collections.Generic;

namespace DAL.Models;

/// <summary>
/// Team Leader: assign tasks to members, monitor task progress | Team Member: view assigned tasks, update task status | Lecturer: view tasks
/// </summary>
public partial class Task
{
    public int TaskId { get; set; }

    public int? RequirementId { get; set; }

    public int? JiraIssueId { get; set; }

    public int? AssignedTo { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public TaskStatus Status { get; set; } = TaskStatus.todo;

    public PriorityLevel Priority { get; set; } = PriorityLevel.medium;

    public DateOnly? DueDate { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual User? AssignedToNavigation { get; set; }

    public virtual JiraIssue? JiraIssue { get; set; }

    public virtual Requirement? Requirement { get; set; }
}
