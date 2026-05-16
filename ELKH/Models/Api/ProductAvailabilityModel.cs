namespace ELKH.Models.Api;

/// <summary>
/// API model for product availability information.
/// Provides stock and availability status for API consumers.
/// </summary>
public class ProductAvailabilityModel
{
    /// <summary>
    /// Product identifier.
    /// </summary>
    public int ProductId { get; set; }

    /// <summary>
    /// Whether the product is currently available for purchase.
    /// </summary>
    public bool IsAvailable { get; set; }

    /// <summary>
    /// Current stock quantity.
    /// </summary>
    public int StockQuantity { get; set; }

    /// <summary>
    /// Whether the stock is considered low.
    /// </summary>
    public bool IsLowStock { get; set; }

    /// <summary>
    /// Human-readable stock status.
    /// </summary>
    public string StockStatus { get; set; } = string.Empty;
}
