using ELKH.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Data;

/// <summary>
/// Database seeding orchestration for the ELKH e-commerce application.
/// This partial class coordinates all domain-specific seeding operations.
/// </summary>
/// <remarks>
/// <para><strong>Table of Contents:</strong></para>
/// <list type="number">
/// <item>Test Transaction Seeding</item>
/// <item>Section 2: Product Catalog Seeding</item>
/// <item>Section 3: Legacy Entry Points</item>
/// </list>
/// 
/// Keeps startup seeding idempotent and delegates domain-specific data creation to partial class files.
/// </remarks>
public static partial class DbSeeder
{
    #region Test Transaction Seeding

    /// <summary>
    /// Seeds test transactions for development and testing purposes.
    /// Creates up to 2 test transactions if they don't already exist.
    /// </summary>
    /// <param name="db">Database context for creating test data.</param>
    public static async Task SeedTestTransactionsAsync(ApplicationDbContext db)
    {
        var user = await db.RegisteredUsers.FirstOrDefaultAsync();
        var product = await db.Products.FirstOrDefaultAsync();
        var contact = await db.ContactDetails.FirstOrDefaultAsync();

        if (user == null || product == null || contact == null) return;

        // Check for existing transactions to maintain idempotency
        int existingTransactions = await db.Transactions.CountAsync();
        int transactionsToCreate = 2 - existingTransactions;

        if (transactionsToCreate <= 0) return;

        for (int i = 0; i < transactionsToCreate; i++)
        {
            var order = new OrderModel
            {
                OrderStatus = OrderStatus.Pending,
                TotalAmount = product.Price,
                CreatedAt = DateTime.UtcNow.AddMinutes(-i * 10),
                DeliveryStatus = DeliveryStatus.Pending,
                FkRegisteredUserId = user.PkRegisteredUserId,
                FkContactId = contact.PkContactId
            };

            db.Orders.Add(order);
            await db.SaveChangesAsync();

            db.OrderItems.Add(new OrderItemModel
            {
                FkOrderId = order.PkOrderId,
                FkProductId = product.PkProductId,
                Quantity = 1
            });

            db.Transactions.Add(new TransactionModel
            {
                TransactionStatus = "Test",
                Amount = product.Price,
                TransactionDate = DateTime.UtcNow.AddMinutes(-i * 10),
                DeliveryFee = 5.99m,
                FkOrderId = order.PkOrderId,
                FkContactId = contact.PkContactId
            });

            await db.SaveChangesAsync();
        }
    }

    #endregion

    #region Section 2: Product Catalog Seeding

    // ===================================================================
    // Section 2: Product Catalog Seeding
    // ===================================================================

    /// <summary>
    /// Seeds the database with 11 product categories and 416 products.
    /// Entirely idempotent-skipped if any products already exist.
    /// </summary>
    /// <param name="db">Database context for inserting categories and products.</param>
    /// <remarks>
    /// <para><strong>Categories Created:</strong></para>
    /// <list type="bullet">
    /// <item>Canadian</item>
    /// <item>Christmas</item>
    /// <item>Cute Animals</item>
    /// <item>Easter</item>
    /// <item>Food</item>
    /// <item>Halloween</item>
    /// <item>Lunar New Year</item>
    /// <item>Nature &amp; Floral</item>
    /// <item>New Years Eve</item>
    /// <item>Thanksgiving</item>
    /// <item>Miscellaneous</item>
    /// </list>
    ///
    /// <para><strong>Product Data:</strong></para>
    /// 416 products are created via <see cref="GetProducts"/> (defined in DbSeeder.Products.cs).
    /// Each product includes Name, NameNormalized (for search), Description, Price, Discount, Stock, and Tags.
    /// NameNormalized is populated during seeding so products are immediately searchable.
    /// </remarks>
    public static async Task SeedProductsAsync(ApplicationDbContext db)
    {
        // ======================================================================
        // ║ Idempotency Check                                                  ║
        // ║ Skip if any products exist-prevents duplicate seeding on restart.  ║
        // ======================================================================
        if (await db.Products.AnyAsync()) return;

        // ======================================================================
        // ║ PHASE 1: Category Creation                                         ║
        // ║ Ensure all 11 categories exist before creating product references. ║
        // ======================================================================
        var categoryNames = new[]
        {
            "Canadian",
            "Christmas",
            "Cute Animals",
            "Easter",
            "Food",
            "Halloween",
            "Lunar New Year",
            "Nature & Floral",
            "New Years Eve",
            "Thanksgiving",
            "Miscellaneous"
        };

        var cats = await db.Categories
            .Where(c => categoryNames.Contains(c.CategoryName))
            .ToDictionaryAsync(c => c.CategoryName);

        var missingCategories = categoryNames
            .Where(name => !cats.ContainsKey(name))
            .Select(name => new CategoryModel { CategoryName = name })
            .ToArray();

        if (missingCategories.Length > 0)
        {
            db.Categories.AddRange(missingCategories);
            await db.SaveChangesAsync();

            foreach (var category in missingCategories)
            {
                cats[category.CategoryName] = category;
            }
        }

        // Load categories into a lookup for the product factory below.

        var canadian = cats["Canadian"];
        var christmas = cats["Christmas"];
        var animals = cats["Cute Animals"];
        var easter = cats["Easter"];
        var food = cats["Food"];
        var halloween = cats["Halloween"];
        var lunarNY = cats["Lunar New Year"];
        var nature = cats["Nature & Floral"];
        var newYear = cats["New Years Eve"];
        var thanks = cats["Thanksgiving"];
        var misc = cats["Miscellaneous"];

        // ======================================================================
        // ║ PHASE 2: Product Creation                                          ║
        // ║ Build 416 products via GetProducts (defined in DbSeeder.Products). ║
        // ======================================================================
        var products = GetProducts(canadian, christmas, animals, easter, food,
                                    halloween, lunarNY, nature, newYear, thanks, misc);

        db.Products.AddRange(products);
        await db.SaveChangesAsync();
    }

    #endregion

    #region Section 3: Legacy Entry Points

    // ===================================================================
    // Section 3: Legacy Entry Points
    // ===================================================================

    /// <summary>
    /// Legacy entry point for admin seeding. 
    /// Redirects to the new decomposed Users and Roles seeding.
    /// </summary>
    /// <param name="db">Database context for creating app-level user records.</param>
    /// <param name="userManager">ASP.NET Core Identity UserManager for account creation.</param>
    /// <param name="roleManager">ASP.NET Core Identity RoleManager for role creation.</param>
    /// <param name="configuration">Application configuration for retrieving credentials.</param>
    /// <param name="wwwRootPath">Web root path for loading the placeholder avatar.</param>
    [Obsolete("Use SeedUsersAndRolesAsync instead. This method is maintained for backward compatibility.")]
    public static async Task SeedAdminAsync(
        ApplicationDbContext db,
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration,
        string wwwRootPath)
    {
        var allowDefaultElevatedCredentials = configuration.GetValue<bool>("Seed:AllowDefaultElevatedCredentials", false);
        await SeedUsersAndRolesAsync(db, userManager, roleManager, configuration, wwwRootPath, allowDefaultElevatedCredentials);
    }

    /// <summary>
    /// Legacy entry point for customer seeding.
    /// Redirects to the new decomposed Customers and Orders seeding.
    /// </summary>
    /// <param name="db">Database context for creating customer-related entities.</param>
    /// <param name="userManager">ASP.NET Core Identity UserManager for creating customer accounts.</param>
    /// <param name="wwwRootPath">Web root path for loading the shared placeholder avatar.</param>
    [Obsolete("Use SeedCustomersAndOrdersAsync instead. This method is maintained for backward compatibility.")]
    public static async Task SeedCustomersAsync(
        ApplicationDbContext db,
        UserManager<IdentityUser> userManager,
        string wwwRootPath)
    {
        await SeedCustomersAndOrdersAsync(db, userManager, wwwRootPath);
    }

    #endregion
}
