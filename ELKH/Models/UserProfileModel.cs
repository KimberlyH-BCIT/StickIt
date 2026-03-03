using System.ComponentModel.DataAnnotations;

namespace ELKH.Models
{
    public class UserProfileModel
    {
        [Key]
        [MaxLength(256)]
        public string PkEmail { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;
    }
}
