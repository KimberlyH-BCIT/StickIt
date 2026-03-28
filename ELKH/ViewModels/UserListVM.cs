namespace ELKH.ViewModels
{
    public class UserListVM
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        // Name- user name
        public string Name { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName     { get; set; }
        public string? PhoneNumber { get; set; }
        public List<string> Roles { get; set; } = new List<string>();
    }
}
