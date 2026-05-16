using ELKH.ViewModels;
using Microsoft.AspNetCore.Identity;

namespace ELKH.Services;

public sealed class AccountDetailsService(
    UserManager<IdentityUser> userManager,
    ApplicationDbContext context) : IAccountDetailsService
{
    public async Task<AccountDetailsVM?> BuildAsync(string identityUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(identityUserId))
        {
            return null;
        }

        var user = await userManager.FindByIdAsync(identityUserId);
        if (user == null)
        {
            return null;
        }

        var roles = await userManager.GetRolesAsync(user);

        var registeredUser = await context.RegisteredUsers
            .FirstOrDefaultAsync(r => r.Email == user.Email, ct);

        var contact = registeredUser is null
            ? null
            : await context.ContactDetails
                .FirstOrDefaultAsync(c => c.FkRegisteredUserId == registeredUser.PkRegisteredUserId, ct);

        return new AccountDetailsVM
        {
            User = new UserListVM
            {
                Id = user.Id,
                Name = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                Roles = roles.ToList()
            },
            Contact = contact == null ? null : new ContactDetailVM
            {
                ContactId = contact.PkContactId,
                FirstName = contact.FirstName,
                LastName = contact.LastName,
                PhoneNumber = contact.PhoneNumber,
                Street = contact.Street,
                City = contact.City,
                Province = contact.Province,
                PostCode = contact.PostCode,
                Country = contact.Country,
                IsDefault = contact.IsDefault
            }
        };
    }
}
