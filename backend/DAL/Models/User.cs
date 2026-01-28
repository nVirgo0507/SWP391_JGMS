using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SWP391_JGMS.DAL.Models;

[Table("USER")]
public class User
{
    [Key]
    [Column("user_id")]
    public int UserId { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("email")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    [Column("password_hash")]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("full_name")]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [Column("role")]
    public UserRole Role { get; set; }

    // Student-specific fields
    [MaxLength(50)]
    [Column("student_code")]
    public string? StudentCode { get; set; }

    [MaxLength(100)]
    [Column("github_username")]
    public string? GithubUsername { get; set; }

    [MaxLength(100)]
    [Column("jira_account_id")]
    public string? JiraAccountId { get; set; }

    // Lecturer-specific fields
    [MaxLength(20)]
    [Column("phone")]
    public string? Phone { get; set; }

    [Column("status")]
    public UserStatus Status { get; set; } = UserStatus.Active;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum UserRole
{
    Admin = 0,
    Lecturer = 1,
    Student = 2
}

public enum UserStatus
{
    Active = 0,
    Inactive = 1
}
