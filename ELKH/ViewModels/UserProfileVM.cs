using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ELKH.ViewModels
{
    /// <summary>
    /// View model for displaying and editing user profile information.
    /// </summary>
    public class UserProfileVM
    {
        /// <summary>User's email address (primary key).</summary>
        [Display(Name = "Email")]
        public string PkEmail { get; set; } = string.Empty;

        /// <summary>User's first name.</summary>
        [Required]
        [Display(Name = "First Name")]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        /// <summary>User's last name.</summary>
        [Required]
        [Display(Name = "Last Name")]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        /// <summary>True when the user has a stored avatar image.</summary>
        public bool HasAvatar { get; set; }
    }

    /// <summary>
    /// Combined view model for the profile page - wraps editable profile fields
    /// alongside the user's read-only list of saved contact/shipping addresses.
    /// </summary>
    public class UserProfilePageVM
    {
        /// <summary>Editable profile information (name fields).</summary>
        public UserProfileVM Profile { get; set; } = new();

        /// <summary>Saved contact/shipping addresses displayed below the profile form.</summary>
        public List<ContactDetailVM> Addresses { get; set; } = [];

        /// <summary>
        /// Optional avatar file submitted via the avatar upload form.
        /// Maximum 10 MB; must be an image (image/jpeg, image/png, image/gif, image/webp).
        /// </summary>
        [Display(Name = "Profile Picture")]
        public IFormFile? AvatarFile { get; set; }
    }
}
