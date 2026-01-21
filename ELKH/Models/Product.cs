using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ELKH.Models
{
    public class Product
    {
        //Product Columns
        [Key]
        public int PkProductId { get; set; }
        [Required]
        public string Name { get; set; } = string.Empty;
        [Required]
        public string Description { get; set; } = string.Empty;
        [Required]
        //DisplayFormat to show 2 decimal places
        [DisplayFormat(DataFormatString = "{0:F2}")]
        public decimal Price { get; set; } = 0;
        [DisplayName("Stock Quantities")]
        public int? StockQuantity { get; set; } = 0;
        [DisplayName("Is Active")]
        public bool IsActive { get; set; } = false;

        //Category Relationship
        //Foreign Key
        public int FkCategoryId { get; set; }
        public Category Categorie { get; set; } = new Category();

        //Product Image Relationship
        public ICollection<ProductImage> ProductImages { get; set; } = new List<ProductImage>();

        //CartItem Relationship
        public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();

        //OriderItem Relationship
        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

        //Product Rating Relationship
        public ICollection<ProductRating> ProductRatings { get; set; } = new List<ProductRating>();
    }
}
