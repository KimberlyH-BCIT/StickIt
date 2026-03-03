using ELKH.Data;
using ELKH.Models;

namespace ELKH.Repositories;

public class RegisteredUserProfileRepo(ApplicationDbContext context)
{
    public IEnumerable<UserProfileModel> GetAll()
        => context.UserProfiles
                  .OrderBy(u => u.PkEmail)
                  .ToList();

    public UserProfileModel? GetById(string email)
        => context.UserProfiles
                  .FirstOrDefault(u => u.PkEmail == email);

    public void Add(UserProfileModel profile)
    {
        // Guard against duplicates
        bool isAnyProfiles = context.UserProfiles
                                    .Any(u => u.PkEmail == profile.PkEmail);
        if (!isAnyProfiles)
        {
            try
            {
                context.UserProfiles.Add(profile);
                context.SaveChanges();
                Console.WriteLine($"UserProfile added successfully for: {profile.PkEmail}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding UserProfile for {profile.PkEmail}: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine($"UserProfile NOT added — a profile already exists for: {profile.PkEmail}");
        }
    }
}
