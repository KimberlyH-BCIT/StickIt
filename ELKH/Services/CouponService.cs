namespace ELKH.Services;

/// <summary>
/// Service for managing discount coupons and promotional campaigns with comprehensive business logic.
/// </summary>
public class CouponService(ApplicationDbContext context, ILogger<CouponService> logger) : ICouponService
{
    #region Validation & Application

    /// <summary>
    /// Validates a coupon code and returns the coupon details if valid.
    /// Performs comprehensive validation including active status, expiration, usage limits, and minimum order requirements.
    /// </summary>
    public async Task<CouponModel?> ValidateCouponAsync(string couponCode, decimal orderSubtotal)
    {
        if (string.IsNullOrWhiteSpace(couponCode))
            return null;

        var normalizedCode = NormalizeCouponCode(couponCode);

        var coupon = await context.Coupons
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Code == normalizedCode);

        if (coupon == null)
        {
            logger.LogInformation("Coupon validation failed: Code '{CouponCode}' not found", couponCode);
            return null;
        }

        // Check if coupon is active
        if (!coupon.IsActive)
        {
            logger.LogInformation("Coupon validation failed: Code '{CouponCode}' is inactive", couponCode);
            return null;
        }

        // Check expiration dates
        if (IsExpired(coupon))
        {
            logger.LogInformation("Coupon validation failed: Code '{CouponCode}' is expired", couponCode);
            return null;
        }

        // Check usage limits
        if (!HasUsageRemaining(coupon))
        {
            logger.LogInformation("Coupon validation failed: Code '{CouponCode}' usage limit exceeded", couponCode);
            return null;
        }

        // Check minimum order value
        if (orderSubtotal < coupon.MinimumOrderValue)
        {
            logger.LogInformation("Coupon validation failed: Order subtotal {Subtotal:C} is below minimum {MinOrder:C} for coupon '{CouponCode}'",
                orderSubtotal, coupon.MinimumOrderValue, couponCode);
            return null;
        }

        logger.LogInformation("Coupon validation successful: Code '{CouponCode}' is valid for order subtotal {Subtotal:C}",
            couponCode, orderSubtotal);
        return coupon;
    }

    /// <summary>
    /// Calculates the discount amount for a given coupon and order details.
    /// Applies business rules including percentage caps, minimum order values, and discount type logic.
    /// </summary>
    public decimal CalculateDiscountAmount(CouponModel coupon, decimal orderSubtotal, decimal shippingCost)
    {
        if (coupon == null) return 0;

        decimal discountAmount = 0;

        switch (coupon.DiscountType.ToUpperInvariant())
        {
            case "PERCENTAGE":
                // Calculate percentage discount
                discountAmount = orderSubtotal * (coupon.DiscountValue / 100m);

                // Apply maximum discount cap if specified
                if (coupon.MaxDiscountAmount.HasValue && discountAmount > coupon.MaxDiscountAmount.Value)
                {
                    discountAmount = coupon.MaxDiscountAmount.Value;
                    logger.LogInformation("Percentage discount capped at {MaxDiscount:C} for coupon '{CouponCode}'",
                        coupon.MaxDiscountAmount.Value, coupon.Code);
                }
                break;

            case "FIXEDAMOUNT":
                // Fixed dollar amount, but not more than order subtotal
                discountAmount = Math.Min(coupon.DiscountValue, orderSubtotal);
                break;

            case "FREESHIPPING":
                // Free shipping - discount equals shipping cost
                discountAmount = shippingCost;
                break;

            default:
                logger.LogWarning("Unknown discount type '{DiscountType}' for coupon '{CouponCode}'",
                    coupon.DiscountType, coupon.Code);
                break;
        }

        // Ensure discount doesn't exceed order subtotal + shipping
        var maxPossibleDiscount = orderSubtotal + (coupon.DiscountType.ToUpperInvariant() == "FREESHIPPING" ? shippingCost : 0);
        discountAmount = Math.Min(discountAmount, maxPossibleDiscount);

        logger.LogInformation("Calculated discount amount {DiscountAmount:C} for coupon '{CouponCode}' on order {Subtotal:C}",
            discountAmount, coupon.Code, orderSubtotal);

        return Math.Max(0, discountAmount); // Ensure non-negative
    }

    #endregion

    #region Usage Tracking

    /// <summary>
    /// Records coupon usage against an order and increments usage tracking.
    /// Should be called after successful order completion to prevent misuse.
    /// </summary>
    public async Task RecordCouponUsageAsync(int couponId, int orderId, decimal discountAmount, string couponCodeUsed)
    {
        // Create usage record
        var orderCoupon = new OrderCouponModel
        {
            FkCouponId = couponId,
            FkOrderId = orderId,
            DiscountAmount = discountAmount,
            CouponCodeUsed = NormalizeCouponCode(couponCodeUsed),
            AppliedAt = DateTime.UtcNow
        };

        context.OrderCoupons.Add(orderCoupon);

        // Increment coupon usage count
        var coupon = await context.Coupons.FindAsync(couponId);
        if (coupon != null)
        {
            coupon.CurrentUsageCount++;
            coupon.UpdatedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();

        logger.LogInformation("Recorded coupon usage: Coupon {CouponId} used in order {OrderId} for discount {DiscountAmount:C}",
            couponId, orderId, discountAmount);
    }

    /// <summary>
    /// Gets coupon usage statistics for analytics and reporting.
    /// </summary>
    public async Task<CouponUsageStats> GetCouponUsageStatsAsync(int couponId)
    {
        var usages = await context.OrderCoupons
            .Where(oc => oc.FkCouponId == couponId)
            .Include(oc => oc.Order)
            .AsNoTracking()
            .ToListAsync();

        if (usages.Count == 0)
        {
            return new CouponUsageStats();
        }

        return new CouponUsageStats
        {
            TotalUses = usages.Count,
            TotalDiscountGiven = usages.Sum(u => u.DiscountAmount),
            AverageOrderValue = usages.Average(u => u.Order.TotalAmount),
            AverageDiscountAmount = usages.Average(u => u.DiscountAmount),
            FirstUsed = usages.Min(u => u.AppliedAt),
            LastUsed = usages.Max(u => u.AppliedAt)
        };
    }

    #endregion

    #region Management Operations

    /// <summary>
    /// Retrieves all active coupons available for customer use.
    /// Excludes expired and usage-exhausted coupons.
    /// </summary>
    public async Task<List<CouponModel>> GetActiveCouponsAsync()
    {
        var now = DateTime.UtcNow;

        return await context.Coupons
            .AsNoTracking()
            .Where(c => c.IsActive &&
                       (c.ValidFrom == null || c.ValidFrom <= now) &&
                       (c.ValidUntil == null || c.ValidUntil >= now) &&
                       (c.UsageLimit == null || c.CurrentUsageCount < c.UsageLimit))
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Retrieves all coupons with optional filtering for management dashboard.
    /// </summary>
    public async Task<List<CouponModel>> GetAllCouponsAsync(bool includeInactive = false, bool includeExpired = false)
    {
        var query = context.Coupons.AsNoTracking().AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        if (!includeExpired)
        {
            var now = DateTime.UtcNow;
            query = query.Where(c => c.ValidUntil == null || c.ValidUntil >= now);
        }

        return await query
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Retrieves a specific coupon by ID for management operations.
    /// </summary>
    public async Task<CouponModel?> GetCouponByIdAsync(int couponId)
    {
        return await context.Coupons
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.PkCouponId == couponId);
    }

    /// <summary>
    /// Creates a new coupon with validation and normalization.
    /// </summary>
    public async Task<CouponModel> CreateCouponAsync(CouponModel coupon)
    {
        // Normalize coupon code
        coupon.Code = NormalizeCouponCode(coupon.Code);
        coupon.CreatedAt = DateTime.UtcNow;
        coupon.UpdatedAt = DateTime.UtcNow;

        // Validate business rules
        ValidateDiscountConfiguration(coupon);

        context.Coupons.Add(coupon);
        await context.SaveChangesAsync();

        logger.LogInformation("Created new coupon: {CouponCode} ({DiscountType}: {DiscountValue})",
            coupon.Code, coupon.DiscountType, coupon.DiscountValue);

        return coupon;
    }

    /// <summary>
    /// Updates an existing coupon with business rule validation.
    /// </summary>
    public async Task UpdateCouponAsync(CouponModel coupon)
    {
        // Normalize coupon code
        coupon.Code = NormalizeCouponCode(coupon.Code);
        coupon.UpdatedAt = DateTime.UtcNow;

        // Validate business rules
        ValidateDiscountConfiguration(coupon);

        context.Coupons.Update(coupon);
        await context.SaveChangesAsync();

        logger.LogInformation("Updated coupon: {CouponCode}", coupon.Code);
    }

    /// <summary>
    /// Deactivates a coupon instead of hard deletion to preserve audit trail.
    /// </summary>
    public async Task DeactivateCouponAsync(int couponId)
    {
        var coupon = await context.Coupons.FindAsync(couponId);
        if (coupon != null)
        {
            coupon.IsActive = false;
            coupon.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();

            logger.LogInformation("Deactivated coupon: {CouponCode}", coupon.Code);
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Normalizes coupon code to uppercase for case-insensitive matching.
    /// </summary>
    private static string NormalizeCouponCode(string code)
    {
        return code?.Trim().ToUpperInvariant() ?? string.Empty;
    }

    /// <summary>
    /// Checks if a coupon is expired based on current date and validity period.
    /// </summary>
    private static bool IsExpired(CouponModel coupon)
    {
        var now = DateTime.UtcNow;
        return (coupon.ValidFrom.HasValue && now < coupon.ValidFrom.Value) ||
               (coupon.ValidUntil.HasValue && now > coupon.ValidUntil.Value);
    }

    /// <summary>
    /// Checks if a coupon has remaining usage based on usage limits.
    /// </summary>
    private static bool HasUsageRemaining(CouponModel coupon)
    {
        return coupon.UsageLimit == null || coupon.CurrentUsageCount < coupon.UsageLimit.Value;
    }

    /// <summary>
    /// Validates discount configuration for business rule compliance.
    /// </summary>
    private static void ValidateDiscountConfiguration(CouponModel coupon)
    {
        switch (coupon.DiscountType.ToUpperInvariant())
        {
            case "PERCENTAGE":
                if (coupon.DiscountValue <= 0 || coupon.DiscountValue > 100)
                    throw new ArgumentException("Percentage discount must be between 1 and 100");
                break;

            case "FIXEDAMOUNT":
                if (coupon.DiscountValue <= 0)
                    throw new ArgumentException("Fixed amount discount must be greater than 0");
                break;

            case "FREESHIPPING":
                // No specific validation needed for free shipping
                break;

            default:
                throw new ArgumentException($"Invalid discount type: {coupon.DiscountType}");
        }

        if (coupon.MinimumOrderValue < 0)
            throw new ArgumentException("Minimum order value cannot be negative");

        if (coupon.MaxDiscountAmount.HasValue && coupon.MaxDiscountAmount.Value <= 0)
            throw new ArgumentException("Maximum discount amount must be greater than 0");

        if (coupon.UsageLimit.HasValue && coupon.UsageLimit.Value <= 0)
            throw new ArgumentException("Usage limit must be greater than 0");

        if (coupon.ValidFrom.HasValue && coupon.ValidUntil.HasValue &&
            coupon.ValidFrom.Value >= coupon.ValidUntil.Value)
            throw new ArgumentException("Valid from date must be before valid until date");
    }

    #endregion
}
