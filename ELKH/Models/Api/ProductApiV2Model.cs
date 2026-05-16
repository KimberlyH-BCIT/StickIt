using System.ComponentModel.DataAnnotations;

namespace ELKH.Models.Api;

/// <summary>
/// Enhanced API model for product data transfer - Version 2.0
/// Includes additional metadata and enriched information.
/// </summary>
public class ProductApiV2Model
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
    /// Product price in cents to avoid floating-point precision issues.
    /// </summary>
    public int PriceInCents { get; set; }

    /// <summary>
    /// Original price before any discounts.
    /// </summary>
    public int OriginalPriceInCents { get; set; }

    /// <summary>
    /// Discount percentage if applicable.
    /// </summary>
    public decimal DiscountPercent { get; set; }

    /// <summary>
    /// Category information.
    /// </summary>
    public ProductCategoryInfo Category { get; set; } = new();

    /// <summary>
    /// Stock information.
    /// </summary>
    public ProductStockInfo Stock { get; set; } = new();

    /// <summary>
    /// Rating and review summary.
    /// </summary>
    public ProductRatingSummary Rating { get; set; } = new();

    /// <summary>
    /// Product image URLs.
    /// </summary>
    public List<string> ImageUrls { get; set; } = new();

    /// <summary>
    /// Product tags/badges.
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Timestamps.
    /// </summary>
    public ProductTimestamps Timestamps { get; set; } = new();
}

public class ProductCategoryInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class ProductStockInfo
{
    public int Quantity { get; set; }
    public bool IsAvailable { get; set; }
    public bool IsLowStock { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class ProductRatingSummary
{
    public decimal Average { get; set; }
    public int Count { get; set; }
    public Dictionary<int, int> Distribution { get; set; } = new();
}

public class ProductTimestamps
{
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
