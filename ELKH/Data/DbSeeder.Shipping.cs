using ELKH.Models;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Data;

/// <summary>
/// Database seeding for infrastructure entities like shipping methods.
/// Part of the decomposed DbSeeder architecture.
/// </summary>
public static partial class DbSeeder
{
    /// <summary>
    /// Seeds the database with default shipping methods and delivery options.
    /// Entirely idempotent-skipped if any shipping methods already exist.
    /// </summary>
    /// <param name="db">Database context for inserting shipping methods.</param>
    /// <remarks>
    /// <para><strong>Shipping Methods Created:</strong></para>
    /// <list type="bullet">
    /// <item><strong>Standard Shipping</strong>: $5.99, 5-7 business days</item>
    /// <item><strong>Express Delivery</strong>: $12.99, 2-3 business days</item>
    /// <item><strong>Priority Overnight</strong>: $19.99, 1-2 business days</item>
    /// </list>
    ///
    /// <para><strong>Free Shipping Threshold:</strong></para>
    /// The free shipping threshold ($50) is handled in the ShippingService business logic,
    /// not at the database level. Standard Shipping is automatically free when cart subtotal
    /// exceeds the threshold.
    ///
    /// <para><strong>Admin Management:</strong></para>
    /// Shipping methods can be enabled/disabled and prices can be updated through the Manager
    /// dashboard without requiring code changes or redeployment.
    /// </remarks>
    public static async Task SeedShippingMethodsAsync(ApplicationDbContext db)
    {
        // ======================================================================
        // ║ Idempotency Check: Skip if shipping methods already exist          ║
        // ======================================================================
        if (await db.ShippingMethods.AnyAsync())
        {
            return;
        }

        // ======================================================================
        // ║ Shipping Method Configuration                                       ║
        // ║ Three tiers: Standard (slow/cheap), Express (balanced), Priority   ║
        // ║ DisplayOrder controls UI sort order (lowest number appears first). ║
        // ======================================================================

        var shippingMethods = new List<ShippingMethodModel>
        {
            new ShippingMethodModel
            {
                Name = "Standard Shipping",
                Description = "Delivery within 5-7 business days",
                BasePrice = 5.99m,
                DeliveryDaysMin = 5,
                DeliveryDaysMax = 7,
                IsActive = true,
                DisplayOrder = 1,
                CreatedAt = DateTime.UtcNow
            },
            new ShippingMethodModel
            {
                Name = "Express Delivery",
                Description = "Delivery within 2-3 business days",
                BasePrice = 12.99m,
                DeliveryDaysMin = 2,
                DeliveryDaysMax = 3,
                IsActive = true,
                DisplayOrder = 2,
                CreatedAt = DateTime.UtcNow
            },
            new ShippingMethodModel
            {
                Name = "Priority Overnight",
                Description = "Delivery within 1-2 business days",
                BasePrice = 19.99m,
                DeliveryDaysMin = 1,
                DeliveryDaysMax = 2,
                IsActive = true,
                DisplayOrder = 3,
                CreatedAt = DateTime.UtcNow
            }
        };

        await db.ShippingMethods.AddRangeAsync(shippingMethods);
        await db.SaveChangesAsync();
    }
}
