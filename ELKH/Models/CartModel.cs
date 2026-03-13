using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELKH.Models
{
    /// <summary>
    /// Represents a single item in a user's active shopping cart.
    /// Stores the quantity and pre-computed line total so the cart view
    /// does not need to recalculate on every render.
    /// </summary>
    public class CartModel
    {
        /// <summary>Primary key for this cart line item.</summary>
        [Key]
        public int PkCartId { get; set; }

        /// <summary>Number of units of the product in the cart.</summary>
        public int Quantity { get; set; } = 1;

        /// <summary>Pre-computed line total (<c>effective unit price × Quantity</c>).</summary>
        public decimal TotalPrice { get; set; }

        /// <summary>Foreign key to the owning registered user.</summary>
        public int? FkRegisteredUserId { get; set; }

        /// <summary>Navigation property to the owning registered user.</summary>
        public RegisteredUserModel RegisteredUser { get; set; } = null!;

        /// <summary>Foreign key to the product in this cart line.</summary>
        public int FkProductID { get; set; }
        public ProductModel? Product { get; set; }
    }
}
    