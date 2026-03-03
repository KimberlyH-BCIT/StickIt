using ELKH.Data;
using ELKH.Models;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Repositories
{
    public class ContactDetailRepo
    {
        private readonly ApplicationDbContext _context;

        public ContactDetailRepo(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<ContactDetailModel>> GetAllByUserIdAsync(int userId)
        {
            return await _context.ContactDetails
                .Where(c => c.FkRegisteredUserId == userId)
                .OrderByDescending(c => c.IsDefault)
                .ThenBy(c => c.PkContactId)
                .ToListAsync();
        }

        public async Task<ContactDetailModel?> GetByIdAsync(int contactId)
        {
            return await _context.ContactDetails
                .FirstOrDefaultAsync(c => c.PkContactId == contactId);
        }

        public async Task<ContactDetailModel?> GetDefaultByUserIdAsync(int userId)
        {
            return await _context.ContactDetails
                .FirstOrDefaultAsync(c => c.FkRegisteredUserId == userId && c.IsDefault);
        }

        public async Task<bool> AddAsync(ContactDetailModel contact)
        {
            try
            {
                // If this is being set as default, unset other defaults for this user
                if (contact.IsDefault)
                {
                    await UnsetOtherDefaultsAsync(contact.FkRegisteredUserId, contact.PkContactId);
                }

                _context.ContactDetails.Add(contact);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> UpdateAsync(ContactDetailModel contact)
        {
            try
            {
                var existing = await GetByIdAsync(contact.PkContactId);
                if (existing is null)
                    return false;

                // If this is being set as default, unset other defaults for this user
                if (contact.IsDefault && !existing.IsDefault)
                {
                    await UnsetOtherDefaultsAsync(contact.FkRegisteredUserId, contact.PkContactId);
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

                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> DeleteAsync(int contactId)
        {
            try
            {
                var contact = await GetByIdAsync(contactId);
                if (contact is null)
                    return false;

                _context.ContactDetails.Remove(contact);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task UnsetOtherDefaultsAsync(int userId, int exceptContactId)
        {
            var otherDefaults = await _context.ContactDetails
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
