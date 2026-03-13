namespace ELKH.ViewModels
{
   
        public class AccountDetailsVM
        {
            public UserListVM User { get; set; } = new UserListVM();
            public ContactDetailVM? Contact { get; set; }
        }
    
}
