using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELKH.Models;

/// <summary>
/// Represents a discount coupon that can be applied to orders for promotional purposes.
/// Supports various discount types, usage limits, and expiration rules.
/// </summary>
/// <remarks>
/// TABLE OF CONTENTS (120 lines)
/// ================================================================================
/// 1. Primary Key & Basic Properties ......................... Lines   25-55
///    - PkCouponId, Code, Name, Description
/// 
/// 2. Discount Configuration ................................. Lines   57-85
///    - DiscountType, DiscountValue, MinimumOrderValue
///    - FreeShipping, MaxDiscountAmount (for percentage caps)
/// 
/// 3. Usage Rules & Validation ............................... Lines   87-105
///    - IsActive, UsageLimit, CurrentUsageCount
///    - ValidFrom, ValidUntil for time-based restrictions
/// 
/// 4. Audit & Tracking ....................................... Lines  107-115
///    - CreatedAt, UpdatedAt for management tracking
/// 
/// 5. Navigation Properties ................................... Lines  117-120
///    - OrderCoupons (many-to-many via junction table)
/// ================================================================================
/// 
/// DISCOUNT TYPES SUPPORTED:
/// • Percentage: 10% off entire order
/// • FixedAmount: $5 off order total
/// • FreeShipping: Waive shipping costs
/// • BOGO: Buy one get one (future enhancement)
/// 
/// BUSINESS RULES:
/// • Coupon codes are case-insensitive and normalized to uppercase
/// • Usage limits prevent abuse (single-use, limited quantity, unlimited)
/// • Minimum order values ensure profitability
/// • Time-based validity prevents expired coupon usage
/// • Maximum discount caps prevent excessive percentage discounts
/// </remarks>
public class CouponModel
{
    /// <summary>
    /// Primary key for the coupon.
    /// </summary>
    [Key]
    public int PkCouponId { get; set; }

    /// <summary>
    /// Unique coupon code entered by customers (e.g., "SAVE10", "WELCOME25").
    /// Case-insensitive, stored in uppercase for consistency.
    /// </summary>
    [Required]
    [MaxLength(50)]
    [Display(Name = "Coupon Code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the coupon shown to customers and managers.
    /// </summary>
    [Required]
    [MaxLength(100)]
    [Display(Name = "Coupon Name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description explaining the coupon offer (e.g., "10% off your entire order").
    /// </summary>
    [MaxLength(500)]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    /// <summary>
    /// Type of discount offered by this coupon.
    /// Values: "Percentage", "FixedAmount", "FreeShipping"
    /// </summary>
    [Required]
    [MaxLength(20)]
    [Display(Name = "Discount Type")]
    public string DiscountType { get; set; } = string.Empty;

    /// <summary>
    /// Discount value based on DiscountType:
    /// - Percentage: Value between 1-100 (e.g., 10 for 10% off)
    /// - FixedAmount: Dollar amount (e.g., 5.00 for $5 off)
    /// - FreeShipping: Ignored (set to 0)
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    [Display(Name = "Discount Value")]
    public decimal DiscountValue { get; set; }

    /// <summary>
    /// Minimum order subtotal required to use this coupon.
    /// Prevents abuse on very small orders.
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    [Display(Name = "Minimum Order Value")]
    public decimal MinimumOrderValue { get; set; } = 0;

    /// <summary>
    /// Maximum discount amount for percentage-based coupons.
    /// Prevents excessive discounts on large orders (e.g., 10% off capped at $50).
    /// Ignored for fixed amount and free shipping coupons.
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    [Display(Name = "Maximum Discount")]
    public decimal? MaxDiscountAmount { get; set; }

    /// <summary>
    /// Whether this coupon is currently available for use.
    /// Inactive coupons cannot be applied to orders.
    /// </summary>
    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Maximum number of times this coupon can be used across all customers.
    /// Null = unlimited usage, 1 = single use, 100 = limited campaign.
    /// </summary>
    [Display(Name = "Usage Limit")]
    public int? UsageLimit { get; set; }

    /// <summary>
    /// Number of times this coupon has been used.
    /// Incremented when orders are completed, used to enforce usage limits.
    /// </summary>
    [Display(Name = "Times Used")]
    public int CurrentUsageCount { get; set; } = 0;

    /// <summary>
    /// Earliest date this coupon can be used (inclusive).
    /// Null = no start date restriction.
    /// </summary>
    [Display(Name = "Valid From")]
    public DateTime? ValidFrom { get; set; }

    /// <summary>
    /// Latest date this coupon can be used (inclusive).
    /// Null = no expiration date.
    /// </summary>
    [Display(Name = "Valid Until")]
    public DateTime? ValidUntil { get; set; }

    /// <summary>
    /// Timestamp when this coupon was created.
    /// </summary>
    [Display(Name = "Created At")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when this coupon was last updated.
    /// </summary>
    [Display(Name = "Updated At")]
    public DateTime? UpdatedAt { get; set; }

    // Navigation Properties
    /// <summary>
    /// Orders that used this coupon (many-to-many via OrderCouponModel).
    /// </summary>
    public virtual ICollection<OrderCouponModel> OrderCoupons { get; set; } = new List<OrderCouponModel>();
}