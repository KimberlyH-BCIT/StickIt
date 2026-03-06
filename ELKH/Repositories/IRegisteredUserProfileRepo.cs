using ELKH.Models;

namespace ELKH.Repositories
{
    /// <summary>
    /// Contract for user profile (first/last name, avatar) data access.
    /// The profile record is keyed by email and created automatically on registration.
    /// </summary>
    public interface IRegisteredUserProfileRepo
    {
        /// <summary>Returns the profile for the given email address, or <see langword="null"/> if none exists.</summary>
        UserProfileModel? GetById(string email);

        /// <summary>
        /// Persists a new profile, silently skipping the operation if a profile
        /// for this email already exists (idempotent insert).
        /// </summary>
        void Add(UserProfileModel profile);

        /// <summary>Applies all changes in <paramref name="profile"/> and saves. Returns <see langword="true"/> on success.</summary>
        bool UpdateAndSave(UserProfileModel profile);
    }
}
