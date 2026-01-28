﻿using Microsoft.EntityFrameworkCore;
using SWP391_JGMS.DAL.Models;

namespace SWP391_JGMS.DAL;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure User entity
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.StudentCode).IsUnique();
            entity.HasIndex(e => e.Role);
            entity.HasIndex(e => e.GithubUsername);

            // Map enums to PostgreSQL enums - Npgsql handles the conversion automatically
            entity.Property(e => e.Role)
                .HasColumnType("user_role");

            entity.Property(e => e.Status)
                .HasColumnType("user_status");
        });
    }
}