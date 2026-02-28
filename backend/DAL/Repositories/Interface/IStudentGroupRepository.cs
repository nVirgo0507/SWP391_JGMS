﻿using DAL.Models;

namespace DAL.Repositories.Interface
{
    public interface IStudentGroupRepository
    {
        Task<StudentGroup?> GetByIdAsync(int groupId);
        Task<StudentGroup?> GetByGroupCodeAsync(string groupCode);
        Task<List<StudentGroup>> GetAllAsync();
        Task<List<StudentGroup>> GetByLecturerIdAsync(int lecturerId);
        System.Threading.Tasks.Task AddAsync(StudentGroup group);
        System.Threading.Tasks.Task UpdateAsync(StudentGroup group);
        System.Threading.Tasks.Task DeleteAsync(int groupId);
        Task<bool> GroupCodeExistsAsync(string groupCode);
        Task<bool> CanDeleteGroupAsync(int groupId);
        Task<StudentGroup?> GetGroupWithDetailsAsync(int groupId);
    }
}
