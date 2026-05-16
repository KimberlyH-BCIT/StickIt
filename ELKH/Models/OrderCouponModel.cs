using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELKH.Models;

/// <summary>
/// Junction table linking orders to coupons used for discount tracking.
/// Records which coupons were applied to specific orders with the actual discount amount.
/// </summary>
/// <remarks>
/// PURPOSE:
/// • Track coupon usage for analytics and reporting
/// • Store actual discount amount applied (may differ due to caps or promotions)
/// • Audit trail for customer service and refund calculations
/// • Support for multiple coupons per order (future enhancement)
/// 
/// BUSINESS RULES:
/// • One record per coupon per order (composite unique key)
/// • Discount amount stored separately from coupon definition for audit
/// • Created timestamp tracks when discount was applied
/// • Cannot be modified after order completion
/// </remarks>
public class OrderCouponModel
{
    /// <summary>
    /// Primary key for the order-coupon relationship.
    /// </summary>
    [Key]
    public int PkOrderCouponId { get; set; }

    /// <summary>
    /// Foreign key to the order that used the coupon.
    /// </summary>
    public int FkOrderId { get; set; }

    /// <summary>
    /// Foreign key to the coupon that was applied.
    /// </summary>
    public int FkCouponId { get; set; }

    /// <summary>
    /// Actual discount amount applied to the order.
    /// May differ from coupon definition due to caps, minimum order rules, or rounding.
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    [Display(Name = "Discount Applied")]
    public decimal DiscountAmount { get; set; }

    /// <summary>
    /// Coupon code as it was entered by the customer.
    /// Denormalized for audit purposes even if coupon code is later changed.
    /// </summary>
    [Required]
    [MaxLength(50)]
    [Display(Name = "Coupon Code Used")]
    public string CouponCodeUsed { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the coupon was applied to this order.
    /// </summary>
    [Display(Name = "Applied At")]
    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    /// <summary>
    /// The order that used this coupon.
    /// </summary>
    public virtual OrderModel Order { get; set; } = null!;

    /// <summary>
    /// The coupon that was applied to this order.
    /// </summary>
    public virtual CouponModel Coupon { get; set; } = null!;
}
