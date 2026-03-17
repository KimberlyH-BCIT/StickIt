using System.ComponentModel.DataAnnotations;

namespace ELKH.ViewModels
{
    /// <summary>
    /// View model for displaying and editing product information in views.
    /// Used for product forms, listings, and details.
    /// </summary>
    public class ProductVM
    {
        /// <summary>Product unique identifier.</summary>
        public int ProductId { get; set; }

        /// <summary>Product name as shown to users.</summary>
        [Required(ErrorMessage = "Please enter a product name.")]
        [Display(Name = "Product Name")]
        [StringLength(100, ErrorMessage = "Product name cannot exceed 100 characters.")]
        public string ProductName { get; set; } = string.Empty;

        /// <summary>Product description for details and search.</summary>
        [Required(ErrorMessage = "Please provide a description for the product.")]
        [Display(Name = "Product Description")]
        [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
        public string Description { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Price")]
        [DataType(DataType.Currency)]
        [DisplayFormat(DataFormatString = "{0:C}", ApplyFormatInEditMode = true)]
        [Range(0, double.MaxValue, ErrorMessage = "Price must be non-negative")]
        public decimal Price { get; set; }

        /// <summary>Optional discount percent (0-100).</summary>
        [Display(Name = "Discount %")]
        [Range(0, 100, ErrorMessage = "Discount percent must be between 0 and 100")]
        public decimal DiscountPercent { get; set; } = 0;

        /// <summary>Current inventory quantity.</summary>
        [Display(Name = "Stock Quantity")]
        [Range(0, int.MaxValue, ErrorMessage = "Stock quantity cannot be negative")]
        public int? StockQuantity { get; set; }

        /// <summary>Whether the product is active and visible in the catalog.</summary>
        [Display(Name = "Is Active")]
        public bool IsActive { get; set; }

        /// <summary>Category ID for the product.</summary>
        [Required(ErrorMessage = "Please select a category.")]
        [Display(Name = "Category")]
        public int CategoryId { get; set; }

        /// <summary>Name of the product's category.</summary>
        [Display(Name = "Category Name")]
        public string CategoryName { get; set; } = string.Empty;

        /// <summary>Thumbnail image URL for the product.</summary>
        [Display(Name = "Thumbnail")]
        public string Thumbnail { get; set; } = string.Empty;

        /// <summary>Average customer rating (1-5 stars).</summary>
        [Display(Name = "Average Rating")]
        public double AverageRating { get; set; } = 0;
    }
}