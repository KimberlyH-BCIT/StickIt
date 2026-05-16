namespace ELKH.ViewModels
{
    /// <summary>
    /// View model for user list display providing user information summary
    /// with roles for administrative user management interfaces.
    /// </summary>
    public class UserListVM
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        // Name- user name
        public string Name { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
        public List<string> Roles { get; set; } = new List<string>();
    }
}
