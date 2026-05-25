using ELKH.Models;

namespace ELKH.Services;

/// <summary>
/// Service interface for managing discount coupons and promotional campaigns.
/// Provides validation, application, and usage tracking for customer discounts.
/// </summary>
/// <remarks>
/// COUPON BUSINESS LOGIC:
/// • Validates coupon codes for availability, expiration, and usage limits
/// • Calculates discount amounts with proper business rule application
/// • Tracks coupon usage to prevent abuse and enforce limits
/// • Supports multiple discount types (percentage, fixed amount, free shipping)
/// • Provides analytics and reporting for promotional campaign effectiveness
/// 
/// DISCOUNT TYPES SUPPORTED:
/// • Percentage: 10% off entire order (with optional maximum cap)
/// • FixedAmount: $5 off order total (with minimum purchase requirement)
/// • FreeShipping: Waive shipping costs regardless of order value
/// • Future: BOGO, category-specific, user-specific promotions
/// 
/// SECURITY &amp; FRAUD PREVENTION:
/// • Usage limit enforcement to prevent bulk abuse
/// • Time-based validation to prevent expired coupon usage
/// • Minimum order value requirements to ensure profitability
/// • Audit trail for compliance and customer service inquiries
/// </remarks>
public interface ICouponService
{
    /// <summary>
    /// Validates a coupon code and returns the coupon details if valid.
    /// Checks active status, expiration, usage limits, and minimum order requirements.
    /// </summary>
    /// <param name="couponCode">The coupon code to validate (case-insensitive)</param>
    /// <param name="orderSubtotal">Order subtotal to check minimum purchase requirements</param>
    /// <returns>Valid coupon model if all checks pass, null if invalid</returns>
    Task<CouponModel?> ValidateCouponAsync(string couponCode, decimal orderSubtotal);

    /// <summary>
    /// Calculates the discount amount for a given coupon and order details.
    /// Applies business rules including percentage caps and minimum order values.
    /// </summary>
    /// <param name="coupon">The coupon to apply</param>
    /// <param name="orderSubtotal">Order subtotal before shipping and tax</param>
    /// <param name="shippingCost">Shipping cost to potentially waive</param>
    /// <returns>Calculated discount amount to apply to the order</returns>
    decimal CalculateDiscountAmount(CouponModel coupon, decimal orderSubtotal, decimal shippingCost);

    /// <summary>
    /// Records coupon usage against an order and increments usage tracking.
    /// Should be called after successful order completion to prevent misuse.
    /// </summary>
    /// <param name="couponId">ID of the coupon that was used</param>
    /// <param name="orderId">ID of the order where coupon was applied</param>
    /// <param name="discountAmount">Actual discount amount applied</param>
    /// <param name="couponCodeUsed">Coupon code as entered by customer</param>
    /// <returns>Task representing the async operation</returns>
    Task RecordCouponUsageAsync(int couponId, int orderId, decimal discountAmount, string couponCodeUsed);

    /// <summary>
    /// Retrieves all active coupons available for customer use.
    /// Excludes expired and usage-exhausted coupons.
    /// </summary>
    /// <returns>List of currently usable coupons</returns>
    Task<List<CouponModel>> GetActiveCouponsAsync();

    /// <summary>
    /// Retrieves a specific coupon by ID for management operations.
    /// </summary>
    /// <param name="couponId">The coupon ID to retrieve</param>
    /// <returns>Coupon model if found, null otherwise</returns>
    Task<CouponModel?> GetCouponByIdAsync(int couponId);

    /// <summary>
    /// Retrieves all coupons with optional filtering for management dashboard.
    /// </summary>
    /// <param name="includeInactive">Whether to include inactive coupons</param>
    /// <param name="includeExpired">Whether to include expired coupons</param>
    /// <returns>List of coupons matching the filter criteria</returns>
    Task<List<CouponModel>> GetAllCouponsAsync(bool includeInactive = false, bool includeExpired = false);

    /// <summary>
    /// Creates a new coupon with validation and normalization.
    /// </summary>
    /// <param name="coupon">The coupon to create</param>
    /// <returns>Created coupon with assigned ID</returns>
    Task<CouponModel> CreateCouponAsync(CouponModel coupon);

    /// <summary>
    /// Updates an existing coupon with business rule validation.
    /// </summary>
    /// <param name="coupon">The coupon with updated values</param>
    /// <returns>Task representing the async operation</returns>
    Task UpdateCouponAsync(CouponModel coupon);

    /// <summary>
    /// Deactivates a coupon instead of hard deletion to preserve audit trail.
    /// </summary>
    /// <param name="couponId">ID of the coupon to deactivate</param>
    /// <returns>Task representing the async operation</returns>
    Task DeactivateCouponAsync(int couponId);

    /// <summary>
    /// Gets coupon usage statistics for analytics and reporting.
    /// </summary>
    /// <param name="couponId">ID of the coupon to analyze</param>
    /// <returns>Usage statistics including total uses, total discount, average order value</returns>
    Task<CouponUsageStats> GetCouponUsageStatsAsync(int couponId);
}

/// <summary>
/// Statistics for coupon usage analysis and promotional effectiveness measurement.
/// </summary>
public class CouponUsageStats
{
    public int TotalUses { get; set; }
    public decimal TotalDiscountGiven { get; set; }
    public decimal AverageOrderValue { get; set; }
    public decimal AverageDiscountAmount { get; set; }
    public DateTime? FirstUsed { get; set; }
    public DateTime? LastUsed { get; set; }
}
