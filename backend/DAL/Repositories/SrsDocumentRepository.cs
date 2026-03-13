﻿using DAL.Models;
using DAL.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repositories
{
    public class SrsDocumentRepository : ISrsDocumentRepository
    {
        private readonly JgmsContext _context;

        public SrsDocumentRepository(JgmsContext context)
        {
            _context = context;
        }

        public async System.Threading.Tasks.Task<List<SrsDocument>> GetByProjectIdAsync(int projectId)
        {
            return await _context.SrsDocuments
                .Include(s => s.GeneratedByNavigation)
                .Include(s => s.Project)
                .Include(s => s.SrsIncludedRequirements)
                    .ThenInclude(r => r.Requirement)
                    .ThenInclude(req => req.JiraIssue)
                .Where(s => s.ProjectId == projectId)
                .OrderByDescending(s => s.GeneratedAt)
                .ToListAsync();
        }

        public async System.Threading.Tasks.Task<SrsDocument?> GetByIdAsync(int documentId)
        {
            return await _context.SrsDocuments
                .Include(s => s.GeneratedByNavigation)
                .Include(s => s.Project)
                .Include(s => s.SrsIncludedRequirements)
                    .ThenInclude(r => r.Requirement)
                    .ThenInclude(req => req.JiraIssue)
                .FirstOrDefaultAsync(s => s.DocumentId == documentId);
        }

        public async System.Threading.Tasks.Task<List<SrsDocument>> GetByUserIdAsync(int userId)
        {
            return await _context.SrsDocuments
                .Include(s => s.GeneratedByNavigation)
                .Include(s => s.Project)
                .Where(s => s.GeneratedBy == userId)
                .OrderByDescending(s => s.GeneratedAt)
                .ToListAsync();
        }

        public async System.Threading.Tasks.Task<bool> ExistsByVersionAsync(int projectId, string version)
        {
            return await _context.SrsDocuments
                .AnyAsync(s => s.ProjectId == projectId && s.Version == version);
        }

        public async System.Threading.Tasks.Task AddAsync(SrsDocument document)
        {
            _context.SrsDocuments.Add(document);
            await _context.SaveChangesAsync();
        }

        public async System.Threading.Tasks.Task UpdateAsync(SrsDocument document)
        {
            _context.SrsDocuments.Update(document);
            await _context.SaveChangesAsync();
        }

        public async System.Threading.Tasks.Task RemoveIncludedRequirementsAsync(int documentId)
        {
            var existing = await _context.SrsIncludedRequirements
                .Where(r => r.DocumentId == documentId)
                .ToListAsync();
            _context.SrsIncludedRequirements.RemoveRange(existing);
            await _context.SaveChangesAsync();
        }
    }
}
