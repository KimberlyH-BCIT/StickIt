using ELKH.Models;

namespace ELKH.Repositories
{
    public interface IRegisteredUserLogRepo
    {
        Task<IEnumerable<UserLogModel>> GetAllAsync();
        Task<UserLogModel?> GetByIdAsync(int id);
        Task<IEnumerable<UserLogModel>> GetByEmailAsync(string email);
        Task<UserLogModel?> GetActiveLogAsync(string email);
        Task<UserLogModel> StartLogAsync(string email);
        Task<bool> EndLogAsync(int pkLogId);
        Task<bool> CloseDanglingIfAnyAsync(string email);
        Task LogActivityAsync(string email, string activityType, string detail);
    }
}
