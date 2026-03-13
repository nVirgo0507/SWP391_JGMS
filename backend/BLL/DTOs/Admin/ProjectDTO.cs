using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs.Admin
{
    /// <summary>
    /// DTO for creating a project linked to a group.
    /// Each group can only have one project (1:1 relationship).
    /// </summary>
    public class CreateProjectDTO
    {
        /// <summary>
        /// The group code (e.g. "SE1234") or numeric group ID to link this project to.
        /// </summary>
        [Required(ErrorMessage = "Group identifier is required")]
        public string GroupCode { get; set; } = null!;

        [Required(ErrorMessage = "Project name is required")]
        [StringLength(200, ErrorMessage = "Project name cannot exceed 200 characters")]
        public string ProjectName { get; set; } = null!;

        public string? Description { get; set; }

        public DateOnly? StartDate { get; set; }

        public DateOnly? EndDate { get; set; }
    }

    /// <summary>
    /// DTO for updating an existing project. All fields are optional — only provided fields are updated.
    /// </summary>
    public class UpdateProjectDTO
    {
        [StringLength(200, ErrorMessage = "Project name cannot exceed 200 characters")]
        public string? ProjectName { get; set; }

        public string? Description { get; set; }

        public DateOnly? StartDate { get; set; }

        public DateOnly? EndDate { get; set; }

        /// <summary>
        /// Set to "active" or "completed".
        /// </summary>
        public string? Status { get; set; }
    }
}

