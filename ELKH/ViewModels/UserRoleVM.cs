using System.ComponentModel.DataAnnotations;

namespace ELKH.ViewModels
{
    public class UserRoleVM
    {
        [Required]
        [Display(Name = "Role Name")]
        public string? RoleName { get; set; }

        [Required]
        [Display(Name = "Email Address")]
        public string? Email { get; set; }
    }
}
