using System.ComponentModel.DataAnnotations;

namespace ELKH.Models
{
    /// <summary>
    /// Represents a precomputed fuzzy search suggestion for fast autocomplete and search.
    /// </summary>
    public class FuzzySuggestionModel
    {
        /// <summary>
        /// Unique identifier for the suggestion (primary key).
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Foreign key to the product this suggestion refers to.
        /// </summary>
        public int PkProductId { get; set; }

        /// <summary>
        /// Display name of the product for this suggestion.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Normalized name for search (lowercase, diacritics removed).
        /// </summary>
        public string NameNormalized { get; set; } = string.Empty;

        /// <summary>
        /// Price of the product at the time the suggestion was generated.
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// Thumbnail image URL for the product.
        /// </summary>
        public string Thumbnail { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when the suggestion was created.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
