﻿using DAL.Models;
using DAL.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repositories
{
    public class TaskRepository : ITaskRepository
    {
        private readonly JgmsContext _context;

        public TaskRepository(JgmsContext context)
        {
            _context = context;
        }

        public async System.Threading.Tasks.Task<List<DAL.Models.Task>> GetTasksByUserIdAsync(int userId)
        {
            return await _context.Tasks
                .Include(t => t.AssignedToNavigation)
                .Include(t => t.JiraIssue)
                .Include(t => t.Requirement)
                .Where(t => t.AssignedTo == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
        }

        public async System.Threading.Tasks.Task<DAL.Models.Task?> GetByIdAsync(int taskId)
        {
            return await _context.Tasks
                .Include(t => t.AssignedToNavigation)
                .Include(t => t.JiraIssue)
                .Include(t => t.Requirement)
                .FirstOrDefaultAsync(t => t.TaskId == taskId);
        }

        public async System.Threading.Tasks.Task UpdateAsync(DAL.Models.Task task)
        {
            task.UpdatedAt = DateTime.UtcNow;
            _context.Tasks.Update(task);
            await _context.SaveChangesAsync();
        }

        public async System.Threading.Tasks.Task AddAsync(DAL.Models.Task task)
        {
            task.CreatedAt = DateTime.UtcNow;
            task.UpdatedAt = DateTime.UtcNow;
            await _context.Tasks.AddAsync(task);
            await _context.SaveChangesAsync();
        }

        public async System.Threading.Tasks.Task<DAL.Models.Task?> GetByJiraIssueIdAsync(int jiraIssueId)
        {
            return await _context.Tasks
                .Include(t => t.AssignedToNavigation)
                .Include(t => t.JiraIssue)
                .Include(t => t.Requirement)
                .FirstOrDefaultAsync(t => t.JiraIssueId == jiraIssueId);
        }

        public async System.Threading.Tasks.Task<List<DAL.Models.Task>> GetTasksByRequirementIdAsync(int requirementId)
        {
            return await _context.Tasks
                .Include(t => t.AssignedToNavigation)
                .Include(t => t.JiraIssue)
                .Include(t => t.Requirement)
                .Where(t => t.RequirementId == requirementId)
                .ToListAsync();
        }

        public async System.Threading.Tasks.Task DeleteAsync(int taskId)
        {
            var task = await GetByIdAsync(taskId);
            if (task != null)
            {
                _context.Tasks.Remove(task);
                await _context.SaveChangesAsync();
            }
        }

        public async System.Threading.Tasks.Task<List<DAL.Models.Task>> GetTasksByProjectIdAsync(int projectId)
        {
            return await _context.Tasks
                .Include(t => t.Requirement)
                .Where(t => t.Requirement != null && t.Requirement.ProjectId == projectId)
                .ToListAsync();
        }

        public async System.Threading.Tasks.Task<List<DAL.Models.Task>> GetOverdueTasksByUserIdAsync(int userId)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            return await _context.Tasks
                .Where(t => t.AssignedTo == userId
                    && t.DueDate.HasValue
                    && t.DueDate < today
                    && !t.CompletedAt.HasValue)
                .ToListAsync();
        }

        public async System.Threading.Tasks.Task<int> CountTasksByStatusAsync(int userId, string status)
        {
            // Normalize status string using same logic as UpdateTaskStatusAsync
            // Handles variants: "to do", "to_do", "in-progress", "in progress", "completed", etc.
            var normalized = status
                .Trim()                   // Trim whitespace first
                .ToLower()                // Convert to lowercase
                .Replace(" ", "_")        // "to do" → "to_do"
                .Replace("-", "_");       // "in-progress" → "in_progress"

            // Map common variants to actual enum values
            var statusString = normalized switch
            {
                "to_do" => "todo",
                "completed" => "done",
                _ => normalized
            };

            // Try to parse to enum
            if (Enum.TryParse<DAL.Models.TaskStatus>(statusString, true, out var taskStatus))
            {
                return await _context.Tasks
                    .Where(t => t.AssignedTo == userId && t.Status == taskStatus)
                    .CountAsync();
            }

            // If parsing fails, count all tasks for user (fallback)
            return await _context.Tasks
                .Where(t => t.AssignedTo == userId)
                .CountAsync();
        }
    }
}
