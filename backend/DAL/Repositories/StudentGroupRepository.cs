﻿using DAL.Models;
using DAL.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repositories
{
    public class StudentGroupRepository : IStudentGroupRepository
    {
        private readonly JgmsContext _context;

        public StudentGroupRepository(JgmsContext context)
        {
            _context = context;
        }

        public async Task<StudentGroup?> GetByIdAsync(int groupId)
        {
            return await _context.StudentGroups.FirstOrDefaultAsync(g => g.GroupId == groupId);
        }

        public async Task<StudentGroup?> GetByGroupCodeAsync(string groupCode)
        {
            return await _context.StudentGroups.FirstOrDefaultAsync(g => g.GroupCode == groupCode);
        }

        public async Task<StudentGroup?> GetGroupWithDetailsAsync(int groupId)
        {
            return await _context.StudentGroups
                .Include(g => g.Lecturer)
                .Include(g => g.Leader)
                .Include(g => g.GroupMembers)
                .FirstOrDefaultAsync(g => g.GroupId == groupId);
        }

        public async Task<List<StudentGroup>> GetAllAsync()
        {
            return await _context.StudentGroups
                .Include(g => g.Lecturer)
                .Include(g => g.Leader)
                .Include(g => g.GroupMembers)
                .ToListAsync();
        }

        public async Task<List<StudentGroup>> GetByLecturerIdAsync(int lecturerId)
        {
            return await _context.StudentGroups
                .Include(g => g.Lecturer)
                .Include(g => g.Leader)
                .Include(g => g.GroupMembers)
                .Where(g => g.LecturerId == lecturerId)
                .ToListAsync();
        }

        public async System.Threading.Tasks.Task AddAsync(StudentGroup group)
        {
            _context.StudentGroups.Add(group);
            await _context.SaveChangesAsync();
        }

        public async System.Threading.Tasks.Task UpdateAsync(StudentGroup group)
        {
            group.UpdatedAt = DateTime.UtcNow;
            _context.StudentGroups.Update(group);
            await _context.SaveChangesAsync();
        }

        public async System.Threading.Tasks.Task DeleteAsync(int groupId)
        {
            var group = await GetByIdAsync(groupId);
            if (group != null)
            {
                // Soft delete - set status to inactive
                group.Status = UserStatus.inactive;
                await UpdateAsync(group);
            }
        }

        public async Task<bool> GroupCodeExistsAsync(string groupCode)
        {
            return await _context.StudentGroups.AnyAsync(g => g.GroupCode == groupCode);
        }

        public async Task<bool> CanDeleteGroupAsync(int groupId)
        {
            // Check if group has a project with important data
            var hasProject = await _context.Projects.AnyAsync(p => p.GroupId == groupId);
            return !hasProject;
        }
    }
}
