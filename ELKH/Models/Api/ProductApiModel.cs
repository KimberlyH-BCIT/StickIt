using System.ComponentModel.DataAnnotations;

namespace ELKH.Models.Api;

/// <summary>
/// API model for product data transfer.
/// Simplified product representation for API consumers.
/// </summary>
public class ProductApiModel
{
    /// <summary>
    /// Product identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Product name.
    /// </summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Product description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Product price as decimal for display purposes.
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// Product price in cents to avoid floating-point precision issues.
    /// </summary>
    public int PriceInCents { get; set; }

    /// <summary>
    /// Category identifier.
    /// </summary>
    public int CategoryId { get; set; }

    /// <summary>
    /// Category name.
    /// </summary>
    public string? CategoryName { get; set; }

    /// <summary>
    /// Current stock level.
    /// </summary>
    public int Stock { get; set; }

    /// <summary>
    /// Current stock quantity.
    /// </summary>
    public int StockQuantity { get; set; }

    /// <summary>
    /// Average rating (0.0 to 5.0).
    /// </summary>
    public decimal AverageRating { get; set; }

    /// <summary>
    /// Total number of ratings.
    /// </summary>
    public int RatingCount { get; set; }

    /// <summary>
    /// Product image URLs.
    /// </summary>
    public List<string> ImageUrls { get; set; } = new();

    /// <summary>
    /// Whether the product is currently available for purchase.
    /// </summary>
    public bool IsAvailable { get; set; }

    /// <summary>
    /// When the product was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the product was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}