namespace ELKH.ViewModels
{
    /// <summary>
    /// View model for user account details providing comprehensive account information
    /// display including profile data, contact details, and account settings.
    /// </summary>
    public class AccountDetailsVM
        {
            public UserListVM User { get; set; } = new UserListVM();
            public ContactDetailVM? Contact { get; set; }
        }
    
}
