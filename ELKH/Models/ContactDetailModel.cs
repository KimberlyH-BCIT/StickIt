using System.ComponentModel.DataAnnotations;

namespace ELKH.Models
{
    public class ContactDetailModel
    {
        [Key]
        public int PkContactId { get; set; }
        [Display(Name ="First Name")]
        public string FirstName { get; set; } = string.Empty;
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Province { get; set; } = string.Empty;
        [Display(Name ="Postcode")]
        public string PostCode { get; set; } = string.Empty;
        public string Country { get; set; } = "Canada";
        [Display(Name ="Is Default Address")]
        public bool IsDefault { get; set; } = true;

        //Relationship with RegisiterUser
        public int FkRegisteredUserId { get; set; }
        public RegisteredUserModel RegisiteredUser { get; set; } = new RegisteredUserModel();

        //Relationship with Transaction
        public ICollection<TransactionModel> Transactions { get; set; } = new List<TransactionModel>();

        //Relationship with ORder
        public ICollection<OrderModel> Orders { get; set; } = new List<OrderModel>();
    }
}
