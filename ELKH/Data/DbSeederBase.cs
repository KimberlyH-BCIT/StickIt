using System.Globalization;
using System.Text;
using ELKH.Models;
using Microsoft.AspNetCore.Identity;

namespace ELKH.Data;

public static partial class DbSeeder
{
    private static readonly Random _random = new Random(42);

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
            Name = name,
            NameNormalized = Normalize(name),
            Description = description,
            Price = price,
            DiscountPercent = discountPercent,
            StockQuantity = stock,
            IsActive = true,
            DateAdded = GenerateRandomDate(),
            IsBestSeller = isBestSeller,
            IsTrending = isTrending,
            FkCategoryId = category.PkCategoryId,
            Category = category,
            Tags = tags
        };

    internal static DateTime GenerateRandomDate()
    {
        var now = DateTime.UtcNow;

        var rand = _random.NextDouble();

        int daysAgo;
        if (rand < 0.30)
        {
            daysAgo = _random.Next(0, 31);
        }
        else if (rand < 0.70)
        {
            daysAgo = _random.Next(31, 181);
        }
        else
        {
            daysAgo = _random.Next(181, 731);
        }

        return now.AddDays(-daysAgo);
    }

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

    internal static Random GetRandom() => _random;

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

            await SeedProductsAsync(db);

            await SeedUsersAndRolesAsync(db, userManager, roleManager, configuration, wwwRootPath, allowDefaultElevatedCredentials);

            await SeedCustomersAndOrdersAsync(db, userManager, wwwRootPath);

            await SeedStoreReviewsAsync(db, userManager);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during database seeding: {ex.Message}");
        }
    }
}
