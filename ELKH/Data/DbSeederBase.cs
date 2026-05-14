using ELKH.Models;
using Microsoft.AspNetCore.Identity;
using System.Globalization;
using System.Text;

namespace ELKH.Data;

/// <summary>
/// Base class for database seeding operations providing common utilities and orchestration.
/// </summary>
/// <remarks>
/// Provides shared helpers used by the partial seeder files.
/// </remarks>
public static partial class DbSeeder
{
    #region Section 1: Shared Utilities & Common Infrastructure

    // ===================================================================
    // Section 1: Shared Utilities & Common Infrastructure
    // ===================================================================

    /// <summary>
    /// Random number generator with fixed seed for reproducible seeding results.
    /// Used across all seeding operations for consistent test data generation.
    /// </summary>
    private static readonly Random _random = new Random(42);

    #endregion

    #region Section 2: Product Factory & Data Generation

    // ===================================================================
    // Section 2: Product Factory & Data Generation
    // ===================================================================

    /// <summary>
    /// Shorthand factory method for creating a seed <see cref="ProductModel"/>.
    /// The single-letter name keeps the inline product table readable.
    /// <see cref="ProductModel.NameNormalized"/> is populated via <see cref="Normalize"/>
    /// so every seeded product is immediately searchable without a separate reindex pass.
    /// </summary>
    internal static ProductModel P(
        string name,
        string description,
        decimal price,
        decimal discountPercent,
        int stock,
        CategoryModel category,
        string tags = "",
        bool isBestSeller = false,
        bool isTrending = false) => new()
    {
        Name             = name,
        NameNormalized   = Normalize(name),
        Description      = description,
        Price            = price,
        DiscountPercent  = discountPercent,
        StockQuantity    = stock,
        IsActive         = true,
        DateAdded        = GenerateRandomDate(),
        IsBestSeller     = isBestSeller,
        IsTrending       = isTrending,
        FkCategoryId     = category.PkCategoryId,
        Category         = category,
        Tags             = tags
    };

    /// <summary>
    /// Generates a random date between 2 years ago and now, with higher probability
    /// of recent dates (weighted toward the past 6 months).
    /// </summary>
    /// <returns>A UTC DateTime suitable for product DateAdded fields.</returns>
    internal static DateTime GenerateRandomDate()
    {
        var now = DateTime.UtcNow;

        // 30% chance of being within last 30 days (new arrivals)
        // 40% chance of being within last 6 months
        // 30% chance of being older (up to 2 years)
        var rand = _random.NextDouble();

        int daysAgo;
        if (rand < 0.30)
        {
            // New arrivals: 0-30 days ago
            daysAgo = _random.Next(0, 31);
        }
        else if (rand < 0.70)
        {
            // Recent: 31-180 days ago
            daysAgo = _random.Next(31, 181);
        }
        else
        {
            // Older: 181-730 days ago (2 years)
            daysAgo = _random.Next(181, 731);
        }

        return now.AddDays(-daysAgo);
    }

    #endregion

    #region Section 3: String Normalization & Search Optimization

    // ===================================================================
    // Section 3: String Normalization & Search Optimization
    // ===================================================================

    /// <summary>
    /// Returns a lowercase, diacritic-free copy of <paramref name="name"/> for use as
    /// <see cref="ProductModel.NameNormalized"/> during seeding.
    /// Identical in behaviour to <c>ProductService.NormalizeName()</c>: NFD decomposition
    /// strips combining marks (diacritics), NFC re-composition reassembles the result,
    /// and <c>ToLowerInvariant</c> applies culture-independent case folding.
    /// Both methods must stay in sync if the normalization strategy ever changes.
    /// </summary>
    /// <param name="name">The product name to normalize.</param>
    /// <returns>A normalized string suitable for search operations.</returns>
    internal static string Normalize(string name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;
        var s = name.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in s)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }

    /// <summary>
    /// Gets the shared Random instance for consistent seeding across all domains.
    /// </summary>
    internal static Random GetRandom() => _random;

    #endregion

    #region Section 4: Main Orchestration & Error Handling

    // ===================================================================
    // Section 4: Main Orchestration & Error Handling
    // ===================================================================

    /// <summary>
    /// Main entry point for database seeding. Orchestrates all seeding operations
    /// in the correct order with proper error handling and logging.
    /// </summary>
    /// <param name="db">Database context for seeding operations.</param>
    /// <param name="userManager">ASP.NET Core Identity UserManager for user operations.</param>
    /// <param name="roleManager">ASP.NET Core Identity RoleManager for role operations.</param>
    /// <param name="configuration">Application configuration for credentials.</param>
    /// <param name="wwwRootPath">Web root path for static file access.</param>
    /// <remarks>
    /// Seeding order is critical:
    /// 1. Products & Categories (foundation data)
    /// 2. Users & Roles (authentication setup)
    /// 3. Customers & Orders (business data)
    /// 4. Reviews (user-generated content)
    /// </remarks>
    public static async Task SeedAllAsync(
        ApplicationDbContext db,
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration,
        string wwwRootPath)
    {
        try
        {
            var allowDefaultElevatedCredentials = configuration.GetValue<bool>("Seed:AllowDefaultElevatedCredentials", false);

            // Step 1: Core product catalog
            await SeedProductsAsync(db);
            
            // Step 2: Administrative users and roles
            await SeedUsersAndRolesAsync(db, userManager, roleManager, configuration, wwwRootPath, allowDefaultElevatedCredentials);
            
            // Step 3: Demo customers with orders and contact details
            await SeedCustomersAndOrdersAsync(db, userManager, wwwRootPath);
            
            // Step 4: Store reviews and ratings
            await SeedStoreReviewsAsync(db, userManager);
        }
        catch (Exception ex)
        {
            // Log the error but don't throw - seeding should not prevent app startup
            Console.WriteLine($"Error during database seeding: {ex.Message}");
        }
    }

    #endregion
}
