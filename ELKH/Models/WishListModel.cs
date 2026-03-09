using System.ComponentModel.DataAnnotations;

namespace ELKH.Models
{
    public class WishListModel
    {
        [Key]
        public int PkWishListId { get; set; }

        //Relationship with user
        public int? FkUserId { get; set; }
        public RegisteredUserModel? RegisteredUser { get; set; }

        //Relationship with Product
        public ICollection<ProductModel>? Products { get; set; }
    }
}
