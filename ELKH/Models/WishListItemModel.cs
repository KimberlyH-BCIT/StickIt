using System.ComponentModel.DataAnnotations;

namespace ELKH.Models
{
    /// <summary>
    /// Represents a single product entry in a user's wishlist.
    /// </summary>
    public class WishListItemModel
    {
        /// <summary>
        /// Unique identifier for the wishlist item (primary key).
        /// </summary>
        [Key]
        public int PkWishListItemId { get; set; }

        /// <summary>
        /// Foreign key to the wishlist this item belongs to.
        /// </summary>
        public int FkWishListId { get; set; }

        /// <summary>
        /// Navigation property to the wishlist this item belongs to.
        /// </summary>
        public WishListModel WishList { get; set; } = null!;

        /// <summary>
        /// Foreign key to the product in the wishlist.
        /// </summary>
        public int FkProductId { get; set; }

        /// <summary>
        /// Navigation property to the product in the wishlist.
        /// </summary>
        public ProductModel Product { get; set; } = null!;
        
        /// <summary>
        /// Timestamp when the product was added to the wishlist.
        /// </summary>
        public DateTime DateAdded { get; set; } = DateTime.UtcNow;
    }
}
