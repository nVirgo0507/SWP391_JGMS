﻿using DAL.Models;

namespace DAL.Repositories.Interface
{
    public interface ISrsDocumentRepository
    {
        System.Threading.Tasks.Task<List<SrsDocument>> GetByProjectIdAsync(int projectId);
        System.Threading.Tasks.Task<SrsDocument?> GetByIdAsync(int documentId);
        System.Threading.Tasks.Task<List<SrsDocument>> GetByUserIdAsync(int userId);
        System.Threading.Tasks.Task AddAsync(SrsDocument document);
        System.Threading.Tasks.Task UpdateAsync(SrsDocument document);
    }
}
