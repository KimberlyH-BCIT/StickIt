using System.ComponentModel.DataAnnotations;

namespace ELKH.Models
{
    /// <summary>
    /// Represents a user's profile information (first and last name).
    /// </summary>
    public class UserProfileModel
    {
        /// <summary>
        /// Email address of the user (primary key).
        /// </summary>
        [Key]
        [MaxLength(256)]
        public string PkEmail { get; set; } = string.Empty;

        /// <summary>
        /// User's first name.
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        /// <summary>
        /// User's last name.
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        /// <summary>
        /// Raw bytes of the profile avatar image (stored directly in the database).
        /// Null when the user has not uploaded a picture.
        /// </summary>
        public byte[]? AvatarData { get; set; }

        /// <summary>
        /// MIME type of the stored avatar (e.g. "image/jpeg", "image/png").
        /// Null when no avatar has been uploaded.
        /// </summary>
        [MaxLength(50)]
        public string? AvatarMimeType { get; set; }
    }
}
