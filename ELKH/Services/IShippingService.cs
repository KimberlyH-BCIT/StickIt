using ELKH.Models;

namespace ELKH.Services;

/// <summary>
/// Service interface for managing shipping methods and calculating shipping costs.
/// </summary>
public interface IShippingService
{
    /// <summary>
    /// Retrieves all active shipping methods ordered by display order.
    /// </summary>
    /// <returns>List of active shipping methods available for customer selection.</returns>
    Task<List<ShippingMethodModel>> GetAvailableShippingMethodsAsync();

    /// <summary>
    /// Calculates the shipping cost for a given shipping method and cart subtotal.
    /// Applies free shipping threshold logic.
    /// </summary>
    /// <param name="shippingMethodId">The ID of the selected shipping method.</param>
    /// <param name="cartSubtotal">The cart subtotal (before tax and shipping).</param>
    /// <param name="freeShippingThreshold">The minimum subtotal for free shipping (default: $50).</param>
    /// <returns>The calculated shipping cost (0 if free shipping applies).</returns>
    Task<decimal> CalculateShippingCostAsync(int shippingMethodId, decimal cartSubtotal, decimal freeShippingThreshold = 50m);

    /// <summary>
    /// Retrieves a specific shipping method by ID.
    /// </summary>
    /// <param name="id">The shipping method ID.</param>
    /// <returns>The shipping method if found, otherwise null.</returns>
    Task<ShippingMethodModel?> GetShippingMethodByIdAsync(int id);
}
