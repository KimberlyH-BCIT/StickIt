using System.ComponentModel.DataAnnotations;

namespace ELKH.ViewModels
{
    public class UserProfileVM
    {
        [Display(Name = "Email")]
        public string PkEmail { get; set; } = string.Empty;

        [Required]
        [Display(Name = "First Name")]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Last Name")]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;
    }
}
