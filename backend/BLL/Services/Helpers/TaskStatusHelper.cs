using System;
using DAL.Models;

namespace BLL.Services.Helpers
{
    /// <summary>
    /// Helper for parsing and validating task status transitions.
    /// Business rule: task statuses may only progress forward: todo -> in_progress -> done.
    /// When a backwards transition is attempted this helper throws an exception with the
    /// message: "Invalid status transition. Tasks cannot move backwards.".
    /// </summary>
    internal static class TaskStatusHelper
    {
        /// <summary>
        /// Normalize common status text variants and parse to <see cref="DAL.Models.TaskStatus"/>.
        /// Accepts case-insensitive variants like "To Do", "to_do", "in-progress", "completed".
        /// Throws <see cref="System.Exception"/> when the value is empty or unrecognized.
        /// </summary>
        public static TaskStatus ParseStatus(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                throw new Exception("Invalid status: empty value");
            }

            var normalized = input.Trim().ToLower().Replace(" ", "_").Replace("-", "_");

            normalized = normalized switch
            {
                "to_do" => "todo",
                "completed" => "done",
                _ => normalized
            };

            if (Enum.TryParse<TaskStatus>(normalized, true, out var status))
            {
                return status;
            }

            throw new Exception($"Invalid status: '{input}'. Valid values: 'todo'/'to do', 'in_progress'/'in progress', 'done'/'completed' (case-insensitive)");
        }

        /// <summary>
        /// Enforce forward-only transitions. Throws <see cref="System.Exception"/> with the
        /// standardized message when a backwards transition is attempted.
        /// </summary>
        public static void ValidateForwardTransition(TaskStatus current, TaskStatus next)
        {
            if ((int)next < (int)current)
            {
                throw new Exception("Invalid status transition. Tasks cannot move backwards.");
            }
        }
    }
}
