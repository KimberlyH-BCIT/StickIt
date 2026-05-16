using ELKH.Data;
using ELKH.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ELKH.Repositories
{
    /// <summary>
    /// Repository for contact detail (address) management.
    /// Inherits common CRUD operations and adds custom logic for default address handling.
    /// </summary>
    public class ContactDetailRepo : RepositoryBase<ContactDetailModel, int>, IContactDetailRepo
    {
        public ContactDetailRepo(ApplicationDbContext context, ILogger<ContactDetailRepo> logger)
            : base(context, logger)
        {
        }

        /// <summary>
        /// Get all contact details for a specific user, ordered by default status.
        /// </summary>
        public async Task<IEnumerable<ContactDetailModel>> GetAllByUserIdAsync(int userId)
        {
            return await Context.ContactDetails
                .Where(c => c.FkRegisteredUserId == userId)
                .OrderByDescending(c => c.IsDefault)
                .ThenBy(c => c.PkContactId)
                .ToListAsync();
        }

        /// <summary>
        /// Get the default contact detail for a user.
        /// </summary>
        public async Task<ContactDetailModel?> GetDefaultByUserIdAsync(int userId)
        {
            return await Context.ContactDetails
                .FirstOrDefaultAsync(c => c.FkRegisteredUserId == userId && c.IsDefault);
        }

        /// <summary>
        /// Add a new contact detail with default address logic.
        /// If marked as default, unsets other defaults for the same user.
        /// </summary>
        public override async Task<bool> AddAndSaveAsync(ContactDetailModel contact)
        {
            try
            {
                // Only unset other defaults if this contact belongs to a registered user
                if (contact.IsDefault && contact.FkRegisteredUserId.HasValue)
                {
                    await UnsetOtherDefaultsAsync(contact.FkRegisteredUserId.Value, contact.PkContactId);
                }

                Context.ContactDetails.Add(contact);
                await Context.SaveChangesAsync();
                Logger.LogInformation("Added contact detail for user {UserId}", contact.FkRegisteredUserId);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error adding contact detail for user {UserId}", contact.FkRegisteredUserId);
                return false;
            }
        }

        /// <summary>
        /// Update an existing contact detail with default address logic.
        /// </summary>
        public override async Task<bool> UpdateAndSaveAsync(ContactDetailModel contact)
        {
            try
            {
                var existing = await GetByIdAsync(contact.PkContactId);
                if (existing is null)
                {
                    Logger.LogWarning("Cannot update contact {ContactId} - not found", contact.PkContactId);
                    return false;
                }

                // If this is being set as default, unset other defaults for this user
                if (contact.IsDefault && !existing.IsDefault && contact.FkRegisteredUserId.HasValue)
                {
                    await UnsetOtherDefaultsAsync(contact.FkRegisteredUserId.Value, contact.PkContactId);
                }

                existing.FirstName = contact.FirstName;
                existing.LastName = contact.LastName;
                existing.PhoneNumber = contact.PhoneNumber;
                existing.Street = contact.Street;
                existing.City = contact.City;
                existing.Province = contact.Province;
                existing.PostCode = contact.PostCode;
                existing.Country = contact.Country;
                existing.IsDefault = contact.IsDefault;
                existing.UserId = contact.UserId;

                await Context.SaveChangesAsync();
                Logger.LogInformation("Updated contact detail {ContactId}", contact.PkContactId);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error updating contact detail {ContactId}", contact.PkContactId);
                return false;
            }
        }

        // DeleteAsync is inherited from base and works fine

        /// <summary>
        /// Unset the IsDefault flag on all other contact details for the specified user.
        /// </summary>
        private async Task UnsetOtherDefaultsAsync(int userId, int exceptContactId)
        {
            var otherDefaults = await Context.ContactDetails
                .Where(c => c.FkRegisteredUserId == userId
                         && c.PkContactId != exceptContactId
                         && c.IsDefault)
                .ToListAsync();

            foreach (var contact in otherDefaults)
            {
                contact.IsDefault = false;
            }
        }
    }
}
