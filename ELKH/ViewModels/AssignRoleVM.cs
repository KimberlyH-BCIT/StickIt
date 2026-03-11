using System.ComponentModel.DataAnnotations;

namespace ELKH.ViewModels
{
    public class AssignRoleVM
    {
        [EmailAddress]
        public string? Email { get; set; }

        public string? RoleName { get; set; }

        public bool IsRoleLocked { get; set; }

        public List<RoleVM> Roles { get; set; } = new();

        public string? ReturnTo { get; set; }
        public string? UserId { get; set; }   // nullable — not always known (e.g. from ListRoles)
        public string? RoleId { get; set; }   // nullable — not always known (e.g. from AccountDetails)
    }
}
