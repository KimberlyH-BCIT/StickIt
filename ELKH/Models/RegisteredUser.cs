using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELKH.Models
{
    public class RegisteredUser
    {
        [Key]
        public int PkRegisteredUserId { get; set; }
        [Required]
        [DisplayName("First Name")]
        public string FirstName { get; set; } = string.Empty;
        [Required]
        [DisplayName("Last Name")]
        public string LastName { get; set; } = string.Empty;
        [Required]
        public string Email { get; set; } = string.Empty;


        //Relationship with Cart
        public Cart Cart { get; set; } = new Cart();

        //Relationship with Order
        public ICollection<Order> Orders { get; set; } = new List<Order>();

        //Relationship with Address
        public ICollection<Address> Addresses { get; set; } = new List<Address>();

        //Relationship With ProductRating
        public ICollection<ProductRating> ProductRatings { get; set; } = new List<ProductRating>();
    }
}
