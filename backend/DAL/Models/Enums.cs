﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Models
{
	public enum UserRole
	{
		admin,
		lecturer,
		student
	}

	public enum UserStatus
	{
		active,
		inactive
	}

	public enum TaskStatus
	{
		todo,
		in_progress,
		done
	}

	public enum PriorityLevel
	{
		high,
		medium,
		low
	}

	public enum RequirementType
	{
		functional,
		non_functional
	}

	public enum JiraPriority
	{
		highest,
		high,
		medium,
		low,
		lowest
	}

	public enum DocumentStatus
	{
		draft,
		published
	}

	public enum ProjectStatus
	{
		active,
		completed
	}

	public enum SyncStatus
	{
		pending,
		syncing,
		success,
		failed
	}

	public enum ReportType
	{
		task_assignment,
		task_completion,
		weekly,
		sprint
	}
}
