using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface ILeaderValidationService
    {
        Task ValidateLeaderAccessAsync(int userId, int groupId);
    }
}
