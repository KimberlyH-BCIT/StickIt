using System.ComponentModel.DataAnnotations;

namespace ELKH.Models
{
    public class Address
    {
        [Key]
        public int PkAddressId { get; set; }
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
        public RegisteredUser RegisiteredUser { get; set; } = new RegisteredUser();

        // Delivery Relationship
        public ICollection<Delivery> Deliveries { get; set; } = new List<Delivery>();
    }
}
