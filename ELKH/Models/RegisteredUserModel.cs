using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELKH.Models
{
    /// <summary>
    /// Represents a customer who has registered an account.
    /// Stores the core identity link (email) and navigation properties
    /// to all entities owned by this user.
    /// </summary>
    public class RegisteredUserModel
    {
        /// <summary>Primary key for the registered user.</summary>
        [Key]
        public int PkRegisteredUserId { get; set; }

        [Required]
        public string Email { get; set; } = string.Empty;

        /// <summary>Cart items currently held by this user.</summary>
        public ICollection<CartModel>? Cart { get; set; }

        /// <summary>All orders placed by this user.</summary>
        public ICollection<OrderModel>? Orders { get; set; }

        /// <summary>Saved delivery addresses belonging to this user.</summary>
        public ICollection<ContactDetailModel>? ContactDetails { get; set; }

        /// <summary>Product ratings submitted by this user.</summary>
        public ICollection<ProductRatingModel>? ProductRatings { get; set; }

        /// <summary>
        /// The user's single wishlist.
        /// Not initialised by default — EF Core populates this on load.
        /// Eagerly instantiating navigation properties can trigger recursive
        /// construction and cause a stack overflow.
        /// </summary>
        public WishListModel? WishLists { get; set; }
    }
}
