using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ELKH.Models
{
    public class ProductModel
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
        public int StockQuantity { get; set; } = 0;
        [DisplayName("Is Active")]
        public bool IsActive { get; set; } = false;

        //Category Relationship
        //Foreign Key
        public int FkCategoryId { get; set; }
        public CategoryModel? Category { get; set; }

        //Product Image Relationship
        public ICollection<ProductImageModel>? ProductImages { get; set; }

        //Cart Relationship
        public ICollection<CartModel>? Carts { get; set; }

        //OriderItem Relationship
        public ICollection<OrderItemModel>? OrderItems { get; set; }

        //Product Rating Relationship
        public ICollection<ProductRatingModel>? ProductRatings { get; set; }

        //WishList Relationship
        public int? FkWishListId { get; set; }
        public WishListModel? WishList { get; set; }
    }
}
