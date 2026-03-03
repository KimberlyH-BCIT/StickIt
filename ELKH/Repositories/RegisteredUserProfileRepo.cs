using ELKH.Data;
using ELKH.Models;
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
    public override void Add(UserProfileModel profile)
    {
        // Guard against duplicates
        bool exists = Context.UserProfiles.Any(u => u.PkEmail == profile.PkEmail);

        if (!exists)
        {
            base.AddAndSave(profile);
        }
        else
        {
            Logger.LogWarning("UserProfile NOT added — a profile already exists for: {Email}", profile.PkEmail);
        }
    }
}
