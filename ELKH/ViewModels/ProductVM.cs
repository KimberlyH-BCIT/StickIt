using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace ELKH.ViewModels
{
    public class ProductVM
    {
        public int ProductId { get; set; }

        [Required(ErrorMessage = "Please enter a product name.")]
        [Display(Name = "Product Name")]
        [StringLength(100, ErrorMessage = "Product name cannot exceed 100 characters.")]
        public string ProductName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please provide a description.")]
        [Display(Name = "Product Description")]
        [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter a price.")]
        [Display(Name = "Price")]
        // FIX: Removed ApplyFormatInEditMode = true — it caused the field to render
        // as "$0.00" which the decimal model binder cannot parse, producing a false
        // validation error on the Add Product page before the user types anything.
        [DisplayFormat(DataFormatString = "{0:0.00}")]
        [Range(0, (double)decimal.MaxValue, ErrorMessage = "Price must be non-negative.")]
        public decimal Price { get; set; }

        [Display(Name = "Discount %")]
        [Range(0, 100, ErrorMessage = "Discount must be between 0 and 100.")]
        public decimal DiscountPercent { get; set; } = 0;

        [Display(Name = "Stock Quantity")]
        [Range(0, int.MaxValue, ErrorMessage = "Stock quantity cannot be negative.")]
        public int? StockQuantity { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; }

        [Required(ErrorMessage = "Please select a category.")]
        [Display(Name = "Category")]
        public int CategoryId { get; set; }

        [Display(Name = "Category Name")]
        public string CategoryName { get; set; } = string.Empty;

        [Display(Name = "Thumbnail")]
        public string Thumbnail { get; set; } = string.Empty;

        [Display(Name = "Average Rating")]
        public double AverageRating { get; set; } = 0;

        /// <summary>Date when the product was added to the catalog.</summary>
        [Display(Name = "Date Added")]
        public DateTime DateAdded { get; set; } = DateTime.UtcNow;

        /// <summary>Whether this product is currently trending.</summary>
        [Display(Name = "Trending")]
        public bool IsTrending { get; set; } = false;

        /// <summary>Whether this product is marked as a best seller.</summary>
        [Display(Name = "Best Seller")]
        public bool IsBestSeller { get; set; } = false;

        /// <summary>Whether this is a new arrival (added in the past 30 days).</summary>
        [Display(Name = "New Arrival")]
        public bool IsNewArrival => (DateTime.UtcNow - DateAdded).TotalDays <= 30;

        /// <summary>Whether the product is in stock.</summary>
        public bool IsInStock => (StockQuantity ?? 0) > 0;

        /// <summary>Whether the product has low stock (less than 10 units).</summary>
        public bool IsLowStock => (StockQuantity ?? 0) > 0 && (StockQuantity ?? 0) < 10;

        /// <summary>Whether the product is soft-deleted.</summary>
        [Display(Name = "Is Deleted")]
        public bool IsDeleted { get; set; } = false;
        [ValidateNever]
        /// <summary>List of existing images associated with this product.</summary>
        public List<ProductImageVM> ExistingImages { get; set; } = new();


        public List<ReviewDisplayVM>? ProductReviews { get; set; }

        [ValidateNever]
        // New images chosen by the user in the file picker
        [Display(Name = "Upload Images")]
        public List<IFormFile>? NewImages { get; set; }

        
    }
}
