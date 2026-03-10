using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ELKH.Models
{
    /// <summary>
    /// Represents an image associated with a product, used for galleries and thumbnails.
    /// </summary>
    public class ProductImageModel
    {
        /// <summary>
        /// Unique identifier for the product image (primary key).
        /// </summary>
        [Key]
        public int PkProductImageId { get; set; }

        /// <summary>
        /// URL or path to the product image.
        /// </summary>
        [DisplayName("Product Image Link")]
        public required string ProductImageURL { get; set; }

        /// <summary>
        /// Foreign key to the product this image belongs to.
        /// </summary>
        public int FkProductId { get; set; }

        /// <summary>
        /// Navigation property to the product this image belongs to.
        /// </summary>
        public ProductModel Product { get; set; } = new ProductModel();
    }
}