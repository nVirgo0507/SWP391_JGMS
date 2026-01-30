using Microsoft.EntityFrameworkCore;
using Npgsql;
using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class JgmsContext : DbContext
{
    public JgmsContext()
    {
    }

    public JgmsContext(DbContextOptions<JgmsContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Commit> Commits { get; set; }

    public virtual DbSet<CommitStatistic> CommitStatistics { get; set; }

    public virtual DbSet<GithubCommit> GithubCommits { get; set; }

    public virtual DbSet<GithubIntegration> GithubIntegrations { get; set; }

    public virtual DbSet<GroupMember> GroupMembers { get; set; }

    public virtual DbSet<JiraIntegration> JiraIntegrations { get; set; }

    public virtual DbSet<JiraIssue> JiraIssues { get; set; }

    public virtual DbSet<PersonalTaskStatistic> PersonalTaskStatistics { get; set; }

    public virtual DbSet<ProgressReport> ProgressReports { get; set; }

    public virtual DbSet<Project> Projects { get; set; }

    public virtual DbSet<Requirement> Requirements { get; set; }

    public virtual DbSet<SrsDocument> SrsDocuments { get; set; }

    public virtual DbSet<SrsIncludedRequirement> SrsIncludedRequirements { get; set; }

    public virtual DbSet<StudentGroup> StudentGroups { get; set; }

    public virtual DbSet<Task> Tasks { get; set; }

    public virtual DbSet<TeamCommitSummary> TeamCommitSummaries { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
			NpgsqlConnection.GlobalTypeMapper.MapEnum<UserRole>("user_role");
			NpgsqlConnection.GlobalTypeMapper.MapEnum<UserStatus>("user_status");

			optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=JGMS;Username=admin;Password=123456");
		}
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

		modelBuilder
			.HasPostgresEnum("document_status", new[] { "draft", "published" })
            .HasPostgresEnum("jira_priority", new[] { "highest", "high", "medium", "low", "lowest" })
            .HasPostgresEnum("priority_level", new[] { "high", "medium", "low" })
            .HasPostgresEnum("project_status", new[] { "active", "completed" })
            .HasPostgresEnum("report_type", new[] { "task_assignment", "task_completion", "weekly", "sprint" })
            .HasPostgresEnum("requirement_type", new[] { "functional", "non-functional" })
            .HasPostgresEnum("sync_status", new[] { "pending", "syncing", "success", "failed" })
            .HasPostgresEnum("task_status", new[] { "todo", "in_progress", "done" })
            .HasPostgresEnum("user_role", new[] { "admin", "lecturer", "student" })
            .HasPostgresEnum("user_status", new[] { "active", "inactive" });

        modelBuilder.Entity<Commit>(entity =>
        {
            entity.HasKey(e => e.CommitId).HasName("commit_pkey");

            entity.ToTable("commit", tb => tb.HasComment("Commits linked to students (matched by github_username) - base data for all commit reports"));

            entity.HasIndex(e => e.GithubCommitId, "commit_github_commit_id_key").IsUnique();

            entity.HasIndex(e => e.CommitDate, "idx_commit_date");

            entity.HasIndex(e => e.ProjectId, "idx_commit_project");

            entity.HasIndex(e => e.UserId, "idx_commit_user");

            entity.Property(e => e.CommitId).HasColumnName("commit_id");
            entity.Property(e => e.Additions)
                .HasDefaultValue(0)
                .HasColumnName("additions");
            entity.Property(e => e.ChangedFiles)
                .HasDefaultValue(0)
                .HasColumnName("changed_files");
            entity.Property(e => e.CommitDate)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("commit_date");
            entity.Property(e => e.CommitMessage).HasColumnName("commit_message");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Deletions)
                .HasDefaultValue(0)
                .HasColumnName("deletions");
            entity.Property(e => e.GithubCommitId).HasColumnName("github_commit_id");
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.GithubCommit).WithOne(p => p.Commit)
                .HasForeignKey<Commit>(d => d.GithubCommitId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("commit_github_commit_id_fkey");

            entity.HasOne(d => d.Project).WithMany(p => p.Commits)
                .HasForeignKey(d => d.ProjectId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("commit_project_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.Commits)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("commit_user_id_fkey");
        });

        modelBuilder.Entity<CommitStatistic>(entity =>
        {
            entity.HasKey(e => e.StatId).HasName("commit_statistics_pkey");

            entity.ToTable("commit_statistics", tb => tb.HasComment("Problem 3: Báo cáo đánh giá tần suất và chất lượng các lần commit | Lecturer: view GitHub commit statistics | Team Member: view personal commit statistics"));

            entity.HasIndex(e => e.PeriodStart, "idx_commit_stats_period");

            entity.HasIndex(e => e.ProjectId, "idx_commit_stats_project");

            entity.HasIndex(e => e.UserId, "idx_commit_stats_user");

            entity.HasIndex(e => new { e.ProjectId, e.UserId, e.PeriodStart, e.PeriodEnd }, "unique_stat_period").IsUnique();

            entity.Property(e => e.StatId).HasColumnName("stat_id");
            entity.Property(e => e.AvgCommitSize)
                .HasDefaultValue(0)
                .HasColumnName("avg_commit_size");
            entity.Property(e => e.CommitFrequency)
                .HasDefaultValueSql("0")
                .HasColumnName("commit_frequency");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.PeriodEnd).HasColumnName("period_end");
            entity.Property(e => e.PeriodStart).HasColumnName("period_start");
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.TotalAdditions)
                .HasDefaultValue(0)
                .HasColumnName("total_additions");
            entity.Property(e => e.TotalChangedFiles)
                .HasDefaultValue(0)
                .HasColumnName("total_changed_files");
            entity.Property(e => e.TotalCommits)
                .HasDefaultValue(0)
                .HasColumnName("total_commits");
            entity.Property(e => e.TotalDeletions)
                .HasDefaultValue(0)
                .HasColumnName("total_deletions");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Project).WithMany(p => p.CommitStatistics)
                .HasForeignKey(d => d.ProjectId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("commit_statistics_project_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.CommitStatistics)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("commit_statistics_user_id_fkey");
        });

        modelBuilder.Entity<GithubCommit>(entity =>
        {
            entity.HasKey(e => e.GithubCommitId).HasName("github_commit_pkey");

            entity.ToTable("github_commit", tb => tb.HasComment("Raw commits synced from GitHub API"));

            entity.HasIndex(e => e.CommitSha, "github_commit_commit_sha_key").IsUnique();

            entity.HasIndex(e => e.AuthorUsername, "idx_github_commit_author");

            entity.HasIndex(e => e.CommitDate, "idx_github_commit_date");

            entity.HasIndex(e => e.ProjectId, "idx_github_commit_project");

            entity.HasIndex(e => e.CommitSha, "idx_github_commit_sha");

            entity.Property(e => e.GithubCommitId).HasColumnName("github_commit_id");
            entity.Property(e => e.Additions)
                .HasDefaultValue(0)
                .HasColumnName("additions");
            entity.Property(e => e.AuthorEmail)
                .HasMaxLength(100)
                .HasColumnName("author_email");
            entity.Property(e => e.AuthorUsername)
                .HasMaxLength(100)
                .HasColumnName("author_username");
            entity.Property(e => e.BranchName)
                .HasMaxLength(100)
                .HasColumnName("branch_name");
            entity.Property(e => e.ChangedFiles)
                .HasDefaultValue(0)
                .HasColumnName("changed_files");
            entity.Property(e => e.CommitDate)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("commit_date");
            entity.Property(e => e.CommitMessage).HasColumnName("commit_message");
            entity.Property(e => e.CommitSha)
                .HasMaxLength(255)
                .HasColumnName("commit_sha");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Deletions)
                .HasDefaultValue(0)
                .HasColumnName("deletions");
            entity.Property(e => e.LastSynced)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("last_synced");
            entity.Property(e => e.ProjectId).HasColumnName("project_id");

            entity.HasOne(d => d.Project).WithMany(p => p.GithubCommits)
                .HasForeignKey(d => d.ProjectId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("github_commit_project_id_fkey");
        });

        modelBuilder.Entity<GithubIntegration>(entity =>
        {
            entity.HasKey(e => e.IntegrationId).HasName("github_integration_pkey");

            entity.ToTable("github_integration", tb => tb.HasComment("Admin: configure GitHub integration"));

            entity.HasIndex(e => e.ProjectId, "github_integration_project_id_key").IsUnique();

            entity.HasIndex(e => e.ProjectId, "idx_github_project");

            entity.Property(e => e.IntegrationId).HasColumnName("integration_id");
            entity.Property(e => e.ApiToken)
                .HasMaxLength(255)
                .HasComment("Encrypted token")
                .HasColumnName("api_token");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.LastSync)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("last_sync");
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.RepoName)
                .HasMaxLength(100)
                .HasColumnName("repo_name");
            entity.Property(e => e.RepoOwner)
                .HasMaxLength(100)
                .HasColumnName("repo_owner");
            entity.Property(e => e.RepoUrl)
                .HasMaxLength(255)
                .HasColumnName("repo_url");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Project).WithOne(p => p.GithubIntegration)
                .HasForeignKey<GithubIntegration>(d => d.ProjectId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("github_integration_project_id_fkey");
        });

        modelBuilder.Entity<GroupMember>(entity =>
        {
            entity.HasKey(e => e.MembershipId).HasName("group_member_pkey");

            entity.ToTable("group_member", tb => tb.HasComment("Lecturer: manage students in assigned groups"));

            entity.HasIndex(e => e.GroupId, "idx_group_member_group");

            entity.HasIndex(e => e.UserId, "idx_group_member_user");

            entity.HasIndex(e => new { e.GroupId, e.UserId }, "unique_group_member").IsUnique();

            entity.Property(e => e.MembershipId).HasColumnName("membership_id");
            entity.Property(e => e.GroupId).HasColumnName("group_id");
            entity.Property(e => e.IsLeader)
                .HasDefaultValue(false)
                .HasColumnName("is_leader");
            entity.Property(e => e.JoinedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("joined_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Group).WithMany(p => p.GroupMembers)
                .HasForeignKey(d => d.GroupId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("group_member_group_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.GroupMembers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("group_member_user_id_fkey");
        });

        modelBuilder.Entity<JiraIntegration>(entity =>
        {
            entity.HasKey(e => e.IntegrationId).HasName("jira_integration_pkey");

            entity.ToTable("jira_integration", tb => tb.HasComment("Admin: configure Jira integration"));

            entity.HasIndex(e => e.ProjectId, "idx_jira_project");

            entity.HasIndex(e => e.ProjectId, "jira_integration_project_id_key").IsUnique();

            entity.Property(e => e.IntegrationId).HasColumnName("integration_id");
            entity.Property(e => e.ApiToken)
                .HasMaxLength(255)
                .HasComment("Encrypted token")
                .HasColumnName("api_token");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.JiraEmail)
                .HasMaxLength(100)
                .HasColumnName("jira_email");
            entity.Property(e => e.JiraUrl)
                .HasMaxLength(255)
                .HasColumnName("jira_url");
            entity.Property(e => e.LastSync)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("last_sync");
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.ProjectKey)
                .HasMaxLength(50)
                .HasColumnName("project_key");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Project).WithOne(p => p.JiraIntegration)
                .HasForeignKey<JiraIntegration>(d => d.ProjectId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("jira_integration_project_id_fkey");
        });

        modelBuilder.Entity<JiraIssue>(entity =>
        {
            entity.HasKey(e => e.JiraIssueId).HasName("jira_issue_pkey");

            entity.ToTable("jira_issue", tb => tb.HasComment("Raw issues synced from Jira - source for requirements management"));

            entity.HasIndex(e => e.IssueKey, "idx_jira_issue_key");

            entity.HasIndex(e => e.ProjectId, "idx_jira_issue_project");

            entity.HasIndex(e => e.Status, "idx_jira_issue_status");

            entity.HasIndex(e => e.IssueType, "idx_jira_issue_type");

            entity.HasIndex(e => e.IssueKey, "jira_issue_issue_key_key").IsUnique();

            entity.HasIndex(e => e.JiraId, "jira_issue_jira_id_key").IsUnique();

            entity.Property(e => e.JiraIssueId).HasColumnName("jira_issue_id");
            entity.Property(e => e.AssigneeJiraId)
                .HasMaxLength(100)
                .HasColumnName("assignee_jira_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedDate)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_date");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IssueKey)
                .HasMaxLength(50)
                .HasColumnName("issue_key");
            entity.Property(e => e.IssueType)
                .HasMaxLength(50)
                .HasColumnName("issue_type");
            entity.Property(e => e.JiraId)
                .HasMaxLength(100)
                .HasColumnName("jira_id");
            entity.Property(e => e.LastSynced)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("last_synced");
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasColumnName("status");
            entity.Property(e => e.Summary)
                .HasMaxLength(255)
                .HasColumnName("summary");
            entity.Property(e => e.UpdatedDate)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_date");

            entity.HasOne(d => d.Project).WithMany(p => p.JiraIssues)
                .HasForeignKey(d => d.ProjectId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("jira_issue_project_id_fkey");
        });

        modelBuilder.Entity<PersonalTaskStatistic>(entity =>
        {
            entity.HasKey(e => e.StatId).HasName("personal_task_statistics_pkey");

            entity.ToTable("personal_task_statistics", tb => tb.HasComment("Team Member: view personal task statistics"));

            entity.HasIndex(e => e.ProjectId, "idx_personal_stats_project");

            entity.HasIndex(e => e.UserId, "idx_personal_stats_user");

            entity.HasIndex(e => new { e.UserId, e.ProjectId }, "unique_user_project").IsUnique();

            entity.Property(e => e.StatId).HasColumnName("stat_id");
            entity.Property(e => e.CompletedTasks)
                .HasDefaultValue(0)
                .HasColumnName("completed_tasks");
            entity.Property(e => e.CompletionRate)
                .HasDefaultValueSql("0")
                .HasColumnName("completion_rate");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.InProgressTasks)
                .HasDefaultValue(0)
                .HasColumnName("in_progress_tasks");
            entity.Property(e => e.LastCalculated)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("last_calculated");
            entity.Property(e => e.OverdueTasks)
                .HasDefaultValue(0)
                .HasColumnName("overdue_tasks");
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.TotalTasks)
                .HasDefaultValue(0)
                .HasColumnName("total_tasks");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Project).WithMany(p => p.PersonalTaskStatistics)
                .HasForeignKey(d => d.ProjectId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("personal_task_statistics_project_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.PersonalTaskStatistics)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("personal_task_statistics_user_id_fkey");
        });

        modelBuilder.Entity<ProgressReport>(entity =>
        {
            entity.HasKey(e => e.ReportId).HasName("progress_report_pkey");

            entity.ToTable("progress_report", tb => tb.HasComment("Problem 2: Tổng hợp báo cáo phân công và thực hiện công việc | Lecturer: view project progress reports"));

            entity.HasIndex(e => e.GeneratedAt, "idx_progress_generated");

            entity.HasIndex(e => e.ProjectId, "idx_progress_project");

            entity.Property(e => e.ReportId).HasColumnName("report_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.FilePath)
                .HasMaxLength(255)
                .HasColumnName("file_path");
            entity.Property(e => e.GeneratedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("generated_at");
            entity.Property(e => e.GeneratedBy).HasColumnName("generated_by");
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.ReportData)
                .HasColumnType("jsonb")
                .HasColumnName("report_data");
            entity.Property(e => e.ReportPeriodEnd).HasColumnName("report_period_end");
            entity.Property(e => e.ReportPeriodStart).HasColumnName("report_period_start");
            entity.Property(e => e.Summary).HasColumnName("summary");

            entity.HasOne(d => d.GeneratedByNavigation).WithMany(p => p.ProgressReports)
                .HasForeignKey(d => d.GeneratedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("progress_report_generated_by_fkey");

            entity.HasOne(d => d.Project).WithMany(p => p.ProgressReports)
                .HasForeignKey(d => d.ProjectId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("progress_report_project_id_fkey");
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.ProjectId).HasName("project_pkey");

            entity.ToTable("project", tb => tb.HasComment("One project per group"));

            entity.HasIndex(e => e.GroupId, "idx_project_group");

            entity.HasIndex(e => e.GroupId, "project_group_id_key").IsUnique();

            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.EndDate).HasColumnName("end_date");
            entity.Property(e => e.GroupId).HasColumnName("group_id");
            entity.Property(e => e.ProjectName)
                .HasMaxLength(200)
                .HasColumnName("project_name");
            entity.Property(e => e.StartDate).HasColumnName("start_date");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Group).WithOne(p => p.Project)
                .HasForeignKey<Project>(d => d.GroupId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("project_group_id_fkey");
        });

        modelBuilder.Entity<Requirement>(entity =>
        {
            entity.HasKey(e => e.RequirementId).HasName("requirement_pkey");

            entity.ToTable("requirement", tb => tb.HasComment("Team Leader: manage group requirements (synced from Jira) | Lecturer: view requirements"));

            entity.HasIndex(e => e.JiraIssueId, "idx_requirement_jira");

            entity.HasIndex(e => e.ProjectId, "idx_requirement_project");

            entity.HasIndex(e => e.JiraIssueId, "requirement_jira_issue_id_key").IsUnique();

            entity.HasIndex(e => e.RequirementCode, "requirement_requirement_code_key").IsUnique();

            entity.Property(e => e.RequirementId).HasColumnName("requirement_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.JiraIssueId).HasColumnName("jira_issue_id");
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.RequirementCode)
                .HasMaxLength(50)
                .HasColumnName("requirement_code");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .HasColumnName("title");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.Requirements)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("requirement_created_by_fkey");

            entity.HasOne(d => d.JiraIssue).WithOne(p => p.Requirement)
                .HasForeignKey<Requirement>(d => d.JiraIssueId)
                .HasConstraintName("requirement_jira_issue_id_fkey");

            entity.HasOne(d => d.Project).WithMany(p => p.Requirements)
                .HasForeignKey(d => d.ProjectId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("requirement_project_id_fkey");
        });

        modelBuilder.Entity<SrsDocument>(entity =>
        {
            entity.HasKey(e => e.DocumentId).HasName("srs_document_pkey");

            entity.ToTable("srs_document", tb => tb.HasComment("Header record for the SRS. Content is built by joining with SRS_INCLUDED_REQUIREMENT"));

            entity.HasIndex(e => e.ProjectId, "idx_srs_project");

            entity.HasIndex(e => e.Version, "idx_srs_version");

            entity.Property(e => e.DocumentId).HasColumnName("document_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.DocumentTitle)
                .HasMaxLength(255)
                .HasColumnName("document_title");
            entity.Property(e => e.FilePath)
                .HasMaxLength(255)
                .HasColumnName("file_path");
            entity.Property(e => e.GeneratedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("generated_at");
            entity.Property(e => e.GeneratedBy).HasColumnName("generated_by");
            entity.Property(e => e.Introduction).HasColumnName("introduction");
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.Scope).HasColumnName("scope");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");
            entity.Property(e => e.Version)
                .HasMaxLength(50)
                .HasColumnName("version");

            entity.HasOne(d => d.GeneratedByNavigation).WithMany(p => p.SrsDocuments)
                .HasForeignKey(d => d.GeneratedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("srs_document_generated_by_fkey");

            entity.HasOne(d => d.Project).WithMany(p => p.SrsDocuments)
                .HasForeignKey(d => d.ProjectId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("srs_document_project_id_fkey");
        });

        modelBuilder.Entity<SrsIncludedRequirement>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("srs_included_requirement_pkey");

            entity.ToTable("srs_included_requirement", tb => tb.HasComment("Links specific requirements to an SRS version. Ensures traceability from Jira -> Req -> SRS."));

            entity.HasIndex(e => e.DocumentId, "idx_srs_included_doc");

            entity.HasIndex(e => e.RequirementId, "idx_srs_included_req");

            entity.HasIndex(e => new { e.DocumentId, e.RequirementId }, "unique_doc_req").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.DocumentId).HasColumnName("document_id");
            entity.Property(e => e.RequirementId).HasColumnName("requirement_id");
            entity.Property(e => e.SectionNumber)
                .HasMaxLength(20)
                .HasComment("e.g. 1.1, 2.0 - Order in document")
                .HasColumnName("section_number");
            entity.Property(e => e.SnapshotDescription)
                .HasComment("Description at time of generation")
                .HasColumnName("snapshot_description");
            entity.Property(e => e.SnapshotTitle)
                .HasMaxLength(255)
                .HasComment("Title at time of generation")
                .HasColumnName("snapshot_title");

            entity.HasOne(d => d.Document).WithMany(p => p.SrsIncludedRequirements)
                .HasForeignKey(d => d.DocumentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("srs_included_requirement_document_id_fkey");

            entity.HasOne(d => d.Requirement).WithMany(p => p.SrsIncludedRequirements)
                .HasForeignKey(d => d.RequirementId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("srs_included_requirement_requirement_id_fkey");
        });

        modelBuilder.Entity<StudentGroup>(entity =>
        {
            entity.HasKey(e => e.GroupId).HasName("student_group_pkey");

            entity.ToTable("student_group", tb => tb.HasComment("Admin: manage student groups, assign lecturers to groups"));

            entity.HasIndex(e => e.LeaderId, "idx_group_leader");

            entity.HasIndex(e => e.LecturerId, "idx_group_lecturer");

            entity.HasIndex(e => e.GroupCode, "student_group_group_code_key").IsUnique();

            entity.Property(e => e.GroupId).HasColumnName("group_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.GroupCode)
                .HasMaxLength(50)
                .HasColumnName("group_code");
            entity.Property(e => e.GroupName)
                .HasMaxLength(200)
                .HasColumnName("group_name");
            entity.Property(e => e.LeaderId).HasColumnName("leader_id");
            entity.Property(e => e.LecturerId).HasColumnName("lecturer_id");
            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasColumnType("user_status");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Leader).WithMany(p => p.StudentGroupLeaders)
                .HasForeignKey(d => d.LeaderId)
                .HasConstraintName("student_group_leader_id_fkey");

            entity.HasOne(d => d.Lecturer).WithMany(p => p.StudentGroupLecturers)
                .HasForeignKey(d => d.LecturerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("student_group_lecturer_id_fkey");
        });

        modelBuilder.Entity<Task>(entity =>
        {
            entity.HasKey(e => e.TaskId).HasName("task_pkey");

            entity.ToTable("task", tb => tb.HasComment("Team Leader: assign tasks to members, monitor task progress | Team Member: view assigned tasks, update task status | Lecturer: view tasks"));

            entity.HasIndex(e => e.AssignedTo, "idx_task_assigned");

            entity.HasIndex(e => e.RequirementId, "idx_task_requirement");

            entity.HasIndex(e => e.JiraIssueId, "task_jira_issue_id_key").IsUnique();

            entity.Property(e => e.TaskId).HasColumnName("task_id");
            entity.Property(e => e.AssignedTo).HasColumnName("assigned_to");
            entity.Property(e => e.CompletedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("completed_at");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.DueDate).HasColumnName("due_date");
            entity.Property(e => e.JiraIssueId).HasColumnName("jira_issue_id");
            entity.Property(e => e.RequirementId).HasColumnName("requirement_id");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .HasColumnName("title");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.AssignedToNavigation).WithMany(p => p.Tasks)
                .HasForeignKey(d => d.AssignedTo)
                .HasConstraintName("task_assigned_to_fkey");

            entity.HasOne(d => d.JiraIssue).WithOne(p => p.Task)
                .HasForeignKey<Task>(d => d.JiraIssueId)
                .HasConstraintName("task_jira_issue_id_fkey");

            entity.HasOne(d => d.Requirement).WithMany(p => p.Tasks)
                .HasForeignKey(d => d.RequirementId)
                .HasConstraintName("task_requirement_id_fkey");
        });

        modelBuilder.Entity<TeamCommitSummary>(entity =>
        {
            entity.HasKey(e => e.SummaryId).HasName("team_commit_summary_pkey");

            entity.ToTable("team_commit_summary", tb => tb.HasComment("Team Leader: view team commit summaries (aggregated from COMMIT_STATISTICS)"));

            entity.HasIndex(e => e.SummaryDate, "idx_team_summary_date");

            entity.HasIndex(e => e.ProjectId, "idx_team_summary_project");

            entity.HasIndex(e => new { e.ProjectId, e.SummaryDate }, "unique_project_date").IsUnique();

            entity.Property(e => e.SummaryId).HasColumnName("summary_id");
            entity.Property(e => e.ActiveContributors)
                .HasDefaultValue(0)
                .HasColumnName("active_contributors");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.SummaryData)
                .HasColumnType("jsonb")
                .HasColumnName("summary_data");
            entity.Property(e => e.SummaryDate).HasColumnName("summary_date");
            entity.Property(e => e.TotalAdditions)
                .HasDefaultValue(0)
                .HasColumnName("total_additions");
            entity.Property(e => e.TotalCommits)
                .HasDefaultValue(0)
                .HasColumnName("total_commits");
            entity.Property(e => e.TotalDeletions)
                .HasDefaultValue(0)
                .HasColumnName("total_deletions");

            entity.HasOne(d => d.Project).WithMany(p => p.TeamCommitSummaries)
                .HasForeignKey(d => d.ProjectId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("team_commit_summary_project_id_fkey");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("USER_pkey");

            entity.ToTable("USER", tb => tb.HasComment("All users: Admin, Lecturer, Student"));

            entity.HasIndex(e => e.Email, "USER_email_key").IsUnique();

            entity.HasIndex(e => e.StudentCode, "USER_student_code_key").IsUnique();

            entity.HasIndex(e => e.Email, "idx_user_email");

            entity.HasIndex(e => e.GithubUsername, "idx_user_github_username");

            entity.HasIndex(e => e.StudentCode, "idx_user_student_code");

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .HasColumnName("email");
            entity.Property(e => e.FullName)
                .HasMaxLength(100)
                .HasColumnName("full_name");
            entity.Property(e => e.GithubUsername)
                .HasMaxLength(100)
                .HasColumnName("github_username");
            entity.Property(e => e.JiraAccountId)
                .HasMaxLength(100)
                .HasColumnName("jira_account_id");
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .HasColumnName("password_hash");
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .HasColumnName("phone");
			entity.Property(e => e.Role)
				.HasColumnName("role")
				.HasColumnType("user_role")
				.IsRequired();
			entity.Property(e => e.Status)
				.HasColumnName("status")
				.HasColumnType("user_status")
				.IsRequired(false);
			entity.Property(e => e.StudentCode)
                .HasMaxLength(50)
                .HasColumnName("student_code");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
