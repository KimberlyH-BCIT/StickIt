using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELKH.Models;

/// <summary>
/// Represents a shipping delivery method with pricing and delivery timeframes.
/// Used in checkout flow to allow customers to select preferred shipping speed.
/// </summary>
public class ShippingMethodModel
{
    /// <summary>
    /// Primary key for the shipping method.
    /// </summary>
    [Key]
    public int PkShippingMethodId { get; set; }

    /// <summary>
    /// Display name of the shipping method (e.g., "Standard Shipping", "Express Delivery").
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Detailed description shown to customers (e.g., "Delivery within 5-7 business days").
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Base shipping cost in dollars before any promotions or thresholds.
    /// Use 0.00 for free shipping options.
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(10,2)")]
    public decimal BasePrice { get; set; }

    /// <summary>
    /// Minimum number of business days for delivery.
    /// </summary>
    [Required]
    public int DeliveryDaysMin { get; set; }

    /// <summary>
    /// Maximum number of business days for delivery.
    /// </summary>
    [Required]
    public int DeliveryDaysMax { get; set; }

    /// <summary>
    /// Whether this shipping method is currently available for selection.
    /// Inactive methods are not shown to customers.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Display order for UI sorting (lower numbers appear first).
    /// Recommended: 1 = Standard, 2 = Express, 3 = Priority
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Timestamp when this shipping method was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when this shipping method was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    // Navigation property
    /// <summary>
    /// Orders that selected this shipping method.
    /// </summary>
    public virtual ICollection<OrderModel> Orders { get; set; } = new List<OrderModel>();
}
