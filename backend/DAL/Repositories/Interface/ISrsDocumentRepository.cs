using DAL.Models;

namespace DAL.Repositories.Interface
{
    public interface ISrsDocumentRepository
    {
        Task<List<SrsDocument>> GetByProjectIdAsync(int projectId);
        Task<SrsDocument?> GetByIdAsync(int documentId);
        Task<List<SrsDocument>> GetByUserIdAsync(int userId);
    }
}
