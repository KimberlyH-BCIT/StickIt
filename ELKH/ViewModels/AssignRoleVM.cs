using System.ComponentModel.DataAnnotations;

namespace ELKH.ViewModels
{
    public class AssignRoleVM
    {

        [EmailAddress]
        public string Email { get; set; }

        public string RoleName { get; set; }

        public bool IsRoleLocked { get; set; }

        public List<RoleVM> Roles { get; set; }
    }

}
