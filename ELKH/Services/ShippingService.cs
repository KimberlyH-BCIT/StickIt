using ELKH.Data;
using ELKH.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ELKH.Services;

/// <summary>
/// Service for managing shipping methods and calculating shipping costs with business logic.
/// </summary>
public class ShippingService : IShippingService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ShippingService> _logger;

    public ShippingService(ApplicationDbContext context, ILogger<ShippingService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves all active shipping methods ordered by display order.
    /// </summary>
    /// <returns>List of active shipping methods available for customer selection.</returns>
    /// <remarks>
    /// Only active methods are returned. Inactive methods (IsActive = false) are hidden
    /// from customers but remain in the database for historical order records.
    /// 
    /// DisplayOrder controls the sort: Standard (1), Express (2), Priority (3).
    /// </remarks>
    public async Task<List<ShippingMethodModel>> GetAvailableShippingMethodsAsync()
    {
        _logger.LogDebug("Fetching available shipping methods");

        var methods = await _context.ShippingMethods
            .Where(sm => sm.IsActive)
            .OrderBy(sm => sm.DisplayOrder)
            .AsNoTracking()
            .ToListAsync();

        _logger.LogInformation("Retrieved {Count} active shipping methods", methods.Count);
        return methods;
    }

    /// <summary>
    /// Calculates the shipping cost for a given shipping method and cart subtotal.
    /// Applies free shipping threshold logic for Standard Shipping.
    /// </summary>
    /// <param name="shippingMethodId">The ID of the selected shipping method.</param>
    /// <param name="cartSubtotal">The cart subtotal (before tax and shipping).</param>
    /// <param name="freeShippingThreshold">The minimum subtotal for free Standard shipping (default: $50).</param>
    /// <returns>The calculated shipping cost (0 if free shipping applies to Standard).</returns>
    /// <remarks>
    /// <para><strong>Free Shipping Logic:</strong></para>
    /// â€¢ Applies ONLY to "Standard Shipping" method
    /// â€¢ Requires cart subtotal >= threshold ($50 by default)
    /// â€¢ Express and Priority always charge full base price
    /// 
    /// <para><strong>Example Scenarios:</strong></para>
    /// â€¢ Cart $60 + Standard â†’ $0 (free shipping)
    /// â€¢ Cart $60 + Express â†’ $12.99 (no free shipping for expedited)
    /// â€¢ Cart $30 + Standard â†’ $5.99 (below threshold)
    /// </remarks>
    public async Task<decimal> CalculateShippingCostAsync(int shippingMethodId, decimal cartSubtotal, decimal freeShippingThreshold = 50m)
    {
        if (cartSubtotal < 0)
        {
            throw new ArgumentException("Cart subtotal cannot be negative.", nameof(cartSubtotal));
        }

        var shippingMethod = await GetShippingMethodByIdAsync(shippingMethodId);

        if (shippingMethod == null)
        {
            throw new ArgumentException($"Shipping method {shippingMethodId} was not found.", nameof(shippingMethodId));
        }

        // Free shipping applies ONLY to Standard Shipping when cart meets threshold
        bool isStandardShipping = shippingMethod.Name.Contains("Standard", StringComparison.OrdinalIgnoreCase);
        bool meetsThreshold = cartSubtotal >= freeShippingThreshold;

        if (isStandardShipping && meetsThreshold)
        {
            _logger.LogInformation("Free shipping applied: Cart ${CartSubtotal} >= ${Threshold} with Standard Shipping",
                cartSubtotal, freeShippingThreshold);
            return 0m;
        }

        _logger.LogDebug("Shipping cost: ${ShippingCost} for {ShippingMethod} (Cart: ${CartSubtotal})",
            shippingMethod.BasePrice, shippingMethod.Name, cartSubtotal);

        return shippingMethod.BasePrice;
    }

    /// <summary>
    /// Retrieves a specific shipping method by ID.
    /// </summary>
    /// <param name="id">The shipping method ID.</param>
    /// <returns>The shipping method if found, otherwise null.</returns>
    /// <remarks>
    /// Used during checkout validation to ensure the selected shipping method exists
    /// and to retrieve pricing/delivery information for order confirmation.
    /// </remarks>
    public async Task<ShippingMethodModel?> GetShippingMethodByIdAsync(int id)
    {
        _logger.LogDebug("Fetching shipping method with ID {ShippingMethodId}", id);

        return await _context.ShippingMethods
            .AsNoTracking()
            .FirstOrDefaultAsync(sm => sm.PkShippingMethodId == id);
    }
}
