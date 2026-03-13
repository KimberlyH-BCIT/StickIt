using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ELKH.Models
{
    /// <summary>
    /// Represents a product in the e-commerce catalog.
    /// Products have pricing (with optional discounts), inventory tracking, category associations,
    /// and relationships to cart items, orders, ratings, wishlists, and search suggestions.
    /// </summary>
    public class ProductModel
    {
        /// <summary>
        /// Unique identifier for the product (primary key).
        /// </summary>
        [Key]
        public int PkProductId { get; set; }

        /// <summary>
        /// Display name of the product as shown to customers.
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Normalized version of the product name for search and indexing.
        /// Stored in lowercase with diacritics removed for efficient fuzzy matching.
        /// </summary>
        [MaxLength(100)]
        public string NameNormalized { get; set; } = string.Empty;

        /// <summary>
        /// Detailed product description shown on product detail pages.
        /// </summary>
        [Required]
        [MaxLength(2000)]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Base price of the product before any discount is applied.
        /// Always non-negative. Displayed with 2 decimal places.
        /// </summary>
        [Required]
        [DisplayFormat(DataFormatString = "{0:F2}")]
        public decimal Price { get; set; } = 0;

        /// <summary>
        /// Optional discount percentage (0-100).
        /// Effective price = Price * (1 - DiscountPercent/100)
        /// Example: DiscountPercent = 10 means 10% off
        /// </summary>
        public decimal DiscountPercent { get; set; } = 0;

        /// <summary>
        /// Current inventory quantity. Null or 0 indicates out of stock.
        /// Decremented when orders are placed, incremented when restocked.
        /// </summary>
        [DisplayName("Stock Quantities")]
        [ConcurrencyCheck]
public int? StockQuantity { get; set; } = 0;

/// <summary>
/// Whether the product is visible in the catalog and available for purchase.
/// Inactive products are hidden from customers but retained in the database.
/// </summary>
        [DisplayName("Is Active")]
        public bool IsActive { get; set; } = false;

        // =====================================================================
        // Relationships
        // =====================================================================

        /// <summary>
        /// Foreign key to the product's category (required).
        /// </summary>
        public int FkCategoryId { get; set; }
        /// <summary>
        /// Navigation property to the product's category.
        /// Used for category filtering and display.
        /// </summary>
        public CategoryModel? Category { get; set; }

        /// <summary>
        /// Collection of images associated with this product.
        /// Used for product galleries and thumbnails.
        /// </summary>
        public ICollection<ProductImageModel>? ProductImage { get; set; }

        //Cart Relationship
        public ICollection<CartModel>? Carts { get; set; }

        //OriderItem Relationship
        public ICollection<OrderItemModel>? OrderItems { get; set; }

        //Product Rating Relationship
        public ICollection<ProductRatingModel>? ProductRatings { get; set; }

        /// <summary>
        /// Collection of wishlist entries for this product.
        /// Customers can save products to wishlists for later purchase.
        /// </summary>
        public ICollection<WishListItemModel> WishListItems { get; set; } = new List<WishListItemModel>();

        /// <summary>
        /// Collection of precomputed fuzzy search suggestions for this product.
        /// Used for autocomplete and search performance optimization.
        /// </summary>
        public ICollection<FuzzySuggestionModel> FuzzySuggestions { get; set; } = new List<FuzzySuggestionModel>();
    }
}
