using System.Collections.Generic;
using System.Linq;
using ELKH.Models;
using ELKH.ViewModels;

namespace ELKH.Controllers;

internal static class UserControllerMappingHelpers
{
    public static UserProfileVM? MapProfile(UserProfileModel? profile)
    {
        if (profile is null)
        {
            return null;
        }

        return new UserProfileVM
        {
            PkEmail = profile.PkEmail,
            FirstName = profile.FirstName,
            LastName = profile.LastName
        };
    }

    public static UserProfileVM MapProfilePage(UserProfileModel? profile, string email)
    {
        if (profile is null)
        {
            return new UserProfileVM { PkEmail = email };
        }

        return new UserProfileVM
        {
            PkEmail = profile.PkEmail,
            FirstName = profile.FirstName,
            LastName = profile.LastName,
            HasAvatar = profile.AvatarData is not null
        };
    }

    public static List<ContactDetailVM> MapContactDetails(IEnumerable<ContactDetailModel> addresses)
    {
        return addresses.Select(MapContactDetail).ToList();
    }

    public static ContactDetailVM MapContactDetail(ContactDetailModel contact)
    {
        return new ContactDetailVM
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
        };
    }
}
