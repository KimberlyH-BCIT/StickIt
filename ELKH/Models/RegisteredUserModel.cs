using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELKH.Models
{
    public class RegisteredUserModel
    {
        // Primary key for the registered user
        [Key]
        public int PkRegisteredUserId { get; set; }
        [Required]
        public string Email { get; set; } = string.Empty;

        // User preferred culture and currency (optional)
        public string PreferredCulture { get; set; } = string.Empty;
        public string PreferredCurrency { get; set; } = string.Empty;


        //Relationship with Cart
        public ICollection<CartModel> Cart { get; set; } = new List<CartModel>();

        //Relationship with Order
        public ICollection<OrderModel> Orders { get; set; } = new List<OrderModel>();

        //Relationship with Contact Detail
        public ICollection<ContactDetailModel> ContactDetails { get; set; } = new List<ContactDetailModel>();

        //Relationship With ProductRating
        public ICollection<ProductRatingModel> ProductRatings { get; set; } = new List<ProductRatingModel>();

        // Relationship with WishList (navigation property).
        // Do not instantiate navigation properties by default to avoid recursive construction
        // which can lead to a stack overflow. EF will populate this when the entity is loaded.
        public WishListModel? WishLists { get; set; }
    }
}
