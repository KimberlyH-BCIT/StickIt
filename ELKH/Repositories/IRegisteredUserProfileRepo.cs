using ELKH.Models;

namespace ELKH.Repositories
{
    public interface IRegisteredUserProfileRepo
    {
        UserProfileModel? GetById(string email);
        void Add(UserProfileModel profile);
        bool UpdateAndSave(UserProfileModel profile);
    }
}
