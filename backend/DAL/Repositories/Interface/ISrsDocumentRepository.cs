﻿using DAL.Models;

namespace DAL.Repositories.Interface
{
    public interface ISrsDocumentRepository
    {
        System.Threading.Tasks.Task<List<SrsDocument>> GetByProjectIdAsync(int projectId);
        System.Threading.Tasks.Task<SrsDocument?> GetByIdAsync(int documentId);
        System.Threading.Tasks.Task<List<SrsDocument>> GetByUserIdAsync(int userId);
        System.Threading.Tasks.Task<bool> ExistsByVersionAsync(int projectId, string version);
        System.Threading.Tasks.Task AddAsync(SrsDocument document);
        System.Threading.Tasks.Task UpdateAsync(SrsDocument document);

        /// <summary>
        /// Removes all SRS_INCLUDED_REQUIREMENT rows linked to the given document.
        /// Called before regenerating the requirement snapshot.
        /// </summary>
        System.Threading.Tasks.Task RemoveIncludedRequirementsAsync(int documentId);
    }
}
