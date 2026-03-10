using System.ComponentModel.DataAnnotations;

namespace ELKH.Models
{
    /// <summary>
    /// Represents a user's wishlist, containing a collection of products the user is interested in.
    /// </summary>
    public class WishListModel
    {
        /// <summary>
        /// Unique identifier for the wishlist (primary key).
        /// </summary>
        [Key]
        public int PkWishListId { get; set; }

        /// <summary>
        /// Foreign key to the user who owns this wishlist.
        /// </summary>
        public int FkUserId { get; set; }

        /// <summary>
        /// Navigation property to the registered user who owns this wishlist.
        /// Do not instantiate navigation properties by default to avoid recursive
        /// construction with `RegisteredUserModel` which can cause a stack overflow.
        /// EF will populate this when the entity is loaded.
        /// </summary>
        public RegisteredUserModel? RegisteredUser { get; set; }

        /// <summary>
        /// Collection of wishlist items (join table for products).
        /// </summary>
        public ICollection<WishListItemModel> WishListItems { get; set; } = new List<WishListItemModel>();
        //Relationship with Product
        public ICollection<ProductModel>? Products { get; set; }
    }
}
