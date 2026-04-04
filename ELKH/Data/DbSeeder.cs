using ELKH.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Globalization;

namespace ELKH.Data;

public static partial class DbSeeder
{
    // ================= TEST TRANSACTIONS =================
    public static async Task SeedTestTransactionsAsync(ApplicationDbContext db)
    {
        var user = await db.RegisteredUsers.FirstOrDefaultAsync();
        var product = await db.Products.FirstOrDefaultAsync();
        var contact = await db.ContactDetails.FirstOrDefaultAsync();

        if (user == null || product == null || contact == null) return;

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

    // ================= PRODUCTS =================
    public static async Task SeedProductsAsync(ApplicationDbContext db)
    {
        if (await db.Products.AnyAsync()) return;

        var categoryNames = new[]
        {
            "Canadian","Christmas","Cute Animals","Easter","Food","Halloween",
            "Lunar New Year","Nature & Floral","New Years Eve","Thanksgiving","Miscellaneous",
            "Die-Cut Stickers","Holographic","Waterproof","Sheet Packs",
            "Anime & Pop Culture","Gaming"
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

        var products = GetProducts(
            cats["Canadian"], cats["Christmas"], cats["Cute Animals"], cats["Easter"],
            cats["Food"], cats["Halloween"], cats["Lunar New Year"],
            cats["Nature & Floral"], cats["New Years Eve"],
            cats["Thanksgiving"], cats["Miscellaneous"]
        );

        db.Products.AddRange(products);
        await db.SaveChangesAsync();
    }

    private static ProductModel P(string name, string desc, decimal price, decimal disc, int stock, CategoryModel cat)
    {
        return new()
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
    }



    // ================= ADMIN =================
    public static async Task SeedAdminAsync(
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration)
    {
        const string adminRole = "Admin";

        foreach (var role in new[] { adminRole, "Manager", "Staff", "Customer" })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        var adminEmail = configuration["Seed:AdminEmail"] ?? "admin@stickit.dev";
        var adminPass = configuration["Seed:AdminPass"] ?? "Admin@2025!";

        var admin = await userManager.FindByEmailAsync(adminEmail);

        if (admin is null)
        {
            admin = new IdentityUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };

            await userManager.CreateAsync(admin, adminPass);
        }

        if (!await userManager.IsInRoleAsync(admin, adminRole))
            await userManager.AddToRoleAsync(admin, adminRole);
    }

    // ================= IMAGES =================
    public static async Task SeedProductImagesAsync(
        ApplicationDbContext appDb,
        ImageStoreContext imageDb,
        string wwwRootPath)
    {
        var imagesFolder = Path.Combine(wwwRootPath, "images", "products");
        if (!Directory.Exists(imagesFolder)) return;

        var products = await appDb.Products
            .Include(p => p.ProductImage)
            .ToListAsync();

        foreach (var product in products)
        {
            if (product.ProductImage != null && product.ProductImage.Any()) continue;

            var slug = Normalize(product.Name).Replace(" ", "-");
            var extensions = new[] { ".png", ".jpg", ".jpeg", ".webp", ".gif" };

            string? filePath = null;

            foreach (var ext in extensions)
            {
                var candidate = Path.Combine(imagesFolder, slug + ext);
                if (File.Exists(candidate))
                {
                    filePath = candidate;
                    break;
                }
            }

            if (filePath is null) continue;

            var fileName = Path.GetFileName(filePath);
            var imageBytes = await File.ReadAllBytesAsync(filePath);

            appDb.ProductImage.Add(new ProductImageModel
            {
                FkProductId = product.PkProductId,
                ProductImageURL = $"/images/products/{fileName}"
            });

            imageDb.Images.Add(new ImageModel
            {
                FileName = fileName,
                Description = product.Name,
                FileType = "image/png",
                ImageData = imageBytes,
                FkProductId = product.PkProductId
            });
        }

        await appDb.SaveChangesAsync();
        await imageDb.SaveChangesAsync();
    }
}
