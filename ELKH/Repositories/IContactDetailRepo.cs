using ELKH.Models;

namespace ELKH.Repositories
{
    public interface IContactDetailRepo
    {
        Task<ContactDetailModel?> GetByIdAsync(int id);
        Task<IEnumerable<ContactDetailModel>> GetAllByUserIdAsync(int userId);
        Task<ContactDetailModel?> GetDefaultByUserIdAsync(int userId);
        Task<bool> AddAsync(ContactDetailModel contact);
        Task<bool> UpdateAsync(ContactDetailModel contact);
        Task<bool> DeleteAsync(int id);
    }
}
