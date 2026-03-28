using ELKH.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace ELKH.Data;

public static class DbSeeder
{
    public static async Task SeedTestTransactionsAsync(ApplicationDbContext db)
    {
        var user = await db.RegisteredUsers.FirstOrDefaultAsync();
        var product = await db.Product.FirstOrDefaultAsync();
        var contact = await db.ContactDetails.FirstOrDefaultAsync();

        if (user == null || product == null || contact == null) return;

        // UPDATED: Changed string comparison to check for any transactions instead of "Test" string
        int existingTransactions = await db.Transactions.CountAsync();

        int transactionsToCreate = 2 - existingTransactions;

        if (transactionsToCreate <= 0) return;

        for (int i = 0; i < transactionsToCreate; i++)
        {
            var order = new OrderModel
            {
                // UPDATED: Replaced "Test" string with Enum
                OrderStatus = OrderStatus.Pending,
                TotalAmount = product.Price,
                CreatedAt = DateTime.UtcNow.AddMinutes(-i * 10),
                // UPDATED: Replaced "Pending" string with Enum
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

    public static async Task SeedProductsAsync(ApplicationDbContext db)
    {
        if (await db.Product.AnyAsync()) return;

        var categoryNames = new[]
        {
            "Die-Cut Stickers", "Holographic", "Waterproof", "Sheet Packs",
            "Anime & Pop Culture", "Nature & Floral", "Gaming"
        };

        foreach (var name in categoryNames)
        {
            if (!await db.Categories.AnyAsync(c => c.CategoryName == name))
                db.Categories.Add(new CategoryModel { CategoryName = name });
        }
        await db.SaveChangesAsync();

        var cats = await db.Categories
            .Where(c => categoryNames.Contains(c.CategoryName))
            .ToDictionaryAsync(c => c.CategoryName);

        var products = new List<ProductModel>
        {
            P("Kawaii Cat Die-Cut Sticker", "Adorable kawaii-style cat.", 2.99m, 0, 150, cats["Die-Cut Stickers"]),
            P("Rainbow Galaxy Holographic", "Shifts through spectrum.", 3.99m, 0, 100, cats["Holographic"]),
            P("Ocean Waves Waterproof", "Japanese inspired wave.", 3.49m, 0, 120, cats["Waterproof"])
            // ... (Additional products truncated for brevity, keep your existing list here)
        };

        db.Product.AddRange(products);
        await db.SaveChangesAsync();
    }

    private static ProductModel P(string name, string desc, decimal price, decimal disc, int stock, CategoryModel cat) => new()
    {
        Name = name,
        NameNormalized = Normalize(name),
        Description = desc,
        Price = price,
        DiscountPercent = disc,
        StockQuantity = stock,
        IsActive = true,
        FkCategoryId = cat.PkCategoryId,
        Category = cat
    };

    private static string Normalize(string name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;
        var s = name.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in s)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }

    public static async Task SeedAdminAsync(UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager, IConfiguration configuration)
    {
        const string adminRole = "Admin";
        foreach (var role in new[] { adminRole, "Manager", "Staff", "Customer" })
            if (!await roleManager.RoleExistsAsync(role)) await roleManager.CreateAsync(new IdentityRole(role));

        var adminEmail = configuration["Seed:AdminEmail"] ?? "admin@stickit.dev";
        var adminPass = configuration["Seed:AdminPass"] ?? "Admin@2025!";

        var admin = await userManager.FindByEmailAsync(adminEmail);
        if (admin is null)
        {
            admin = new IdentityUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
            await userManager.CreateAsync(admin, adminPass);
        }
        if (!await userManager.IsInRoleAsync(admin, adminRole)) await userManager.AddToRoleAsync(admin, adminRole);
    }

    public static async Task SeedCustomersAsync(ApplicationDbContext db, UserManager<IdentityUser> userManager, string wwwRootPath)
    {
        if (await db.RegisteredUsers.AnyAsync(u => u.Email.EndsWith("@home.com"))) return;

        var products = await db.Product.AsNoTracking().ToListAsync();
        if (products.Count == 0) return;

        var rng = new Random(42);
        // ... (Keep your existing firstNames, lastNames, locations, street pools here)

        for (int i = 0; i < 50; i++)
        {
            // ... (Keep IdentityUser and RegisteredUserModel creation logic)

            // 7. Orders logic - UPDATED to use Enums and fix variable names
            int orderCount = rng.Next(1, 4);
            for (int o = 0; o < orderCount; o++)
            {
                var orderDate = DateTime.UtcNow.AddDays(-rng.Next(15, 400));
                int roll = rng.Next(10);

                // UPDATED: Now assigns Enum values directly
                var (oStatus, dStatus) = roll switch
                {
                    < 7 => (OrderStatus.Shipped, DeliveryStatus.Shipped),
                    < 9 => (OrderStatus.Pending, DeliveryStatus.InTransit),
                    _ => (OrderStatus.Pending, DeliveryStatus.Pending)
                };

                var order = new OrderModel
                {
                    OrderStatus = oStatus,
                    DeliveryStatus = dStatus,
                    CreatedAt = orderDate,
                    // FkRegisteredUserId and FkContactId assigned here...
                    // TotalAmount will be updated after items are added
                };

                // (Rest of your order item and transaction logic using 'order' variable)
                // UPDATED: Check status using Enum instead of string
                if (oStatus == OrderStatus.Shipped)
                {
                    // Add Transaction logic here...
                }
            }
        }
    }
}