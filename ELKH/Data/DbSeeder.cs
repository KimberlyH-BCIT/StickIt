using ELKH.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Data;

/*
 * ┌────────────────────────────────────────────────────────────────────────────┐
 * │ DECOMPOSED DBSEEDER ARCHITECTURE                                           │
 * ├────────────────────────────────────────────────────────────────────────────┤
 * │ This file has been decomposed into domain-specific seeders:                │
 * │                                                                            │
 * │ • DbSeederBase.cs ......................... Common utilities & orchestration │
 * │ • DbSeeder.Products.cs ................... Product catalog (416 products)  │
 * │ • DbSeeder.Users.cs ...................... Admin/role management          │
 * │ • DbSeeder.Customers.cs .................. Demo customers & orders         │
 * │ • DbSeeder.Reviews.cs .................... Store testimonials              │
 * │                                                                            │
 * │ BENEFITS:                                                                  │
 * │ ✅ Better maintainability - domain separation                             │
 * │ ✅ Reduced file size - easier navigation                                  │
 * │ ✅ Clear responsibilities - single purpose per file                       │
 * │ ✅ Preserved idempotency - all seeding operations safe to repeat          │
 * └────────────────────────────────────────────────────────────────────────────┘
 */

/// <summary>
/// Database seeding orchestration for the ELKH e-commerce application.
/// This partial class coordinates all domain-specific seeding operations.
/// </summary>
/// <remarks>
/// <para><strong>Idempotency Strategy:</strong></para>
/// All seeding methods are fully idempotent—they check for existing data before inserting
/// and can be safely called multiple times. This allows the seeding to be executed on every
/// application startup without creating duplicates.
///
/// <para><strong>Security Considerations:</strong></para>
/// Administrative credentials are read from configuration (user-secrets/environment variables).
/// Default fallback credentials are intentionally weak and intended for local development only.
/// Production deployments MUST override these via Azure Key Vault or environment variables.
///
/// <para><strong>Decomposed Architecture:</strong></para>
/// The seeding logic is split across multiple partial class files:
/// <list type="bullet">
/// <item><strong>DbSeederBase.cs</strong>: Common utilities and main orchestration</item>
/// <item><strong>DbSeeder.Products.cs</strong>: Product catalog seeding (416 products)</item>
/// <item><strong>DbSeeder.Users.cs</strong>: Admin accounts and role management</item>
/// <item><strong>DbSeeder.Customers.cs</strong>: Demo customers with orders and reviews</item>
/// <item><strong>DbSeeder.Reviews.cs</strong>: Store testimonials for homepage</item>
/// </list>
/// </remarks>
public static partial class DbSeeder
{
    #region Product Seeding

    /// <summary>
    /// Seeds the database with 11 product categories and 416 products.
    /// Entirely idempotent—skipped if any products already exist.
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
        // ══════════════════════════════════════════════════════════════════════
        // ║ Idempotency Check                                                  ║
        // ║ Skip if any products exist—prevents duplicate seeding on restart.  ║
        // ══════════════════════════════════════════════════════════════════════
        if (await db.Products.AnyAsync()) return;

        // ══════════════════════════════════════════════════════════════════════
        // ║ PHASE 1: Category Creation                                         ║
        // ║ Ensure all 11 categories exist before creating product references. ║
        // ══════════════════════════════════════════════════════════════════════
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

        foreach (var name in categoryNames)
        {
            if (!await db.Categories.AnyAsync(c => c.CategoryName == name))
                db.Categories.Add(new CategoryModel { CategoryName = name });
        }
        await db.SaveChangesAsync();

        // ── Load Categories into Dictionary ──────────────────────────────────
        // Retrieve all categories with their assigned PKs for foreign key references.
        // Dictionary lookup by name allows clean product creation code below.
        var cats = await db.Categories
            .Where(c => categoryNames.Contains(c.CategoryName))
            .ToDictionaryAsync(c => c.CategoryName);

        var canadian  = cats["Canadian"];
        var christmas = cats["Christmas"];
        var animals   = cats["Cute Animals"];
        var easter    = cats["Easter"];
        var food      = cats["Food"];
        var halloween = cats["Halloween"];
        var lunarNY   = cats["Lunar New Year"];
        var nature    = cats["Nature & Floral"];
        var newYear   = cats["New Years Eve"];
        var thanks    = cats["Thanksgiving"];
        var misc      = cats["Miscellaneous"];

        // ══════════════════════════════════════════════════════════════════════
        // ║ PHASE 2: Product Creation                                          ║
        // ║ Build 416 products via GetProducts (defined in DbSeeder.Products). ║
        // ══════════════════════════════════════════════════════════════════════
        var products = GetProducts(canadian, christmas, animals, easter, food,
                                    halloween, lunarNY, nature, newYear, thanks, misc);

        db.Products.AddRange(products);
        await db.SaveChangesAsync();
    }

    #endregion

    #region Legacy Entry Points (Backward Compatibility)

    /// <summary>
    /// Legacy entry point for admin seeding. 
    /// Redirects to the new decomposed Users and Roles seeding.
    /// </summary>
    /// <param name="userManager">ASP.NET Core Identity UserManager for account creation.</param>
    /// <param name="roleManager">ASP.NET Core Identity RoleManager for role creation.</param>
    /// <param name="configuration">Application configuration for retrieving credentials.</param>
    [Obsolete("Use SeedUsersAndRolesAsync instead. This method is maintained for backward compatibility.")]
    public static async Task SeedAdminAsync(
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration)
    {
        await SeedUsersAndRolesAsync(userManager, roleManager, configuration);
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