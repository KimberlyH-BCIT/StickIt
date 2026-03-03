using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELKH.Models
{
    public class RegisteredUserModel
    {
        [Key]
        public int PkRegisteredUserId { get; set; }
        [Required]
        public string Email { get; set; } = string.Empty;


        //Relationship with Cart
        public ICollection<CartModel> Cart { get; set; } = new List<CartModel>();

        //Relationship with Order
        public ICollection<OrderModel> Orders { get; set; } = new List<OrderModel>();

        //Relationship with Contact Detail
        public ICollection<ContactDetailModel> ContactDetails { get; set; } = new List<ContactDetailModel>();

        //Relationship With ProductRating
        public ICollection<ProductRatingModel> ProductRatings { get; set; } = new List<ProductRatingModel>();

        //Relationship with WishList
        public WishListModel WishLists { get; set; } = new WishListModel();
    }
}
