using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ELKH.Models
{
    /// <summary>
    /// Represents a product category for organizing products in the catalog.
    /// </summary>
    public class CategoryModel
    {
        /// <summary>
        /// Unique identifier for the category (primary key).
        /// </summary>
        [Key]
        public int PkCategoryId { get; set; }

        /// <summary>
        /// Name of the category (e.g., "Electronics", "Books").
        /// </summary>
        [Required]
        [DisplayName("Category Name")]
        public string CategoryName { get; set; } = string.Empty;

        /// <summary>
        /// Collection of products in this category.
        /// </summary>
        public ICollection<ProductModel> Products { get; set; } = new List<ProductModel>();
    }
}
