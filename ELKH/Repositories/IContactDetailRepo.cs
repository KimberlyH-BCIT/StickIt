using ELKH.Models;

namespace ELKH.Repositories
{
    /// <summary>
    /// Contract for contact detail (shipping address) data access.
    /// Supports per-user address books with a single default address flag.
    /// </summary>
    public interface IContactDetailRepo
    {
        /// <summary>Returns a single contact detail by primary key, or <see langword="null"/> if not found.</summary>
        Task<ContactDetailModel?> GetByIdAsync(int id);

        /// <summary>Returns all addresses for a user, sorted with the default address first.</summary>
        Task<IEnumerable<ContactDetailModel>> GetAllByUserIdAsync(int userId);

        /// <summary>Returns the address marked as default for the given user, or <see langword="null"/> if none is set.</summary>
        Task<ContactDetailModel?> GetDefaultByUserIdAsync(int userId);

        /// <summary>
        /// Persists a new address. If <c>IsDefault</c> is <see langword="true"/>,
        /// all other addresses for the same user are unset as default first.
        /// </summary>
        Task<bool> AddAsync(ContactDetailModel contact);

        /// <summary>Updates an existing address, applying the same default-flag enforcement as <see cref="AddAsync"/>.</summary>
        Task<bool> UpdateAsync(ContactDetailModel contact);

        /// <summary>Deletes an address by primary key. Returns <see langword="false"/> if the address does not exist.</summary>
        Task<bool> DeleteAsync(int id);
    }
}
