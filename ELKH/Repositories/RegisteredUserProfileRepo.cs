using ELKH.Data;
using ELKH.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ELKH.Repositories;

/// <summary>
/// Repository for user profile management.
/// Inherits common CRUD operations from RepositoryBase.
/// </summary>
public class RegisteredUserProfileRepo : RepositoryBase<UserProfileModel, string>, IRegisteredUserProfileRepo
{
    public RegisteredUserProfileRepo(ApplicationDbContext context, ILogger<RegisteredUserProfileRepo> logger)
        : base(context, logger)
    {
    }

    // GetAll() and GetById() are inherited from base class

    /// <summary>
    /// Add a new user profile with duplicate prevention.
    /// </summary>
    public override async Task<bool> AddAndSaveAsync(UserProfileModel profile)
    {
        bool exists = await Context.UserProfiles.AnyAsync(u => u.PkEmail == profile.PkEmail);
        if (!exists)
            return await base.AddAndSaveAsync(profile);

        Logger.LogWarning("UserProfile NOT added - a profile already exists for: {Email}", profile.PkEmail);
        return false;
    }

    public void UpdateAndSave(UserProfileModel existing)
    {
        throw new NotImplementedException();
    }
}
