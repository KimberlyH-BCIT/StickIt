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
            P("Kawaii Cat Die-Cut Sticker",     "Adorable kawaii-style cat.",             2.99m, 0,  150, cats["Die-Cut Stickers"]),
            P("Rainbow Galaxy Holographic",      "Shifts through spectrum.",               3.99m, 0,  100, cats["Holographic"]),
            P("Ocean Waves Waterproof",          "Japanese inspired wave.",                3.49m, 0,  120, cats["Waterproof"]),
            P("Floral Sheet Pack",               "10 botanical stickers per sheet.",       5.99m, 10, 80,  cats["Sheet Packs"]),
            P("Anime Eyes Holographic",          "Sparkling anime eye stickers.",          4.49m, 0,  90,  cats["Anime & Pop Culture"]),
            P("Sakura Blossom Set",              "Cherry blossom die-cuts.",               3.99m, 5,  110, cats["Nature & Floral"]),
            P("Pixel Controller Gaming",         "Retro pixel art game controller.",       3.49m, 0,  95,  cats["Gaming"]),
            P("Mushroom Forest Waterproof",      "Whimsical mushroom scene.",              2.99m, 0,  130, cats["Waterproof"]),
            P("Dragon Ball Z Pack",              "Iconic DBZ character stickers.",         5.49m, 15, 60,  cats["Anime & Pop Culture"]),
            P("Succulent Garden Sheet",          "9 succulent varieties per sheet.",       4.99m, 0,  75,  cats["Sheet Packs"]),
            P("Holographic Stars & Moons",       "Celestial holographic shapes.",          3.99m, 0,  105, cats["Holographic"]),
            P("Space Invaders Gaming Sticker",   "Classic arcade invader sticker.",        2.99m, 0,  115, cats["Gaming"]),
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
        if (!await userManager.IsInRoleAsync(admin, adminRole))
            await userManager.AddToRoleAsync(admin, adminRole);
    }

    // =========================================================================
    //  CUSTOMERS  (keeps existing ones, only seeds when none exist)
    // =========================================================================
    public static async Task SeedCustomersAsync(
        ApplicationDbContext db,
        UserManager<IdentityUser> userManager,
        string wwwRootPath)
    {
        if (await db.RegisteredUsers.AnyAsync(u => u.Email.EndsWith("@home.com"))) return;

        var products = await db.Product.AsNoTracking().ToListAsync();
        if (products.Count == 0) return;

        var rng = new Random(42);

        string[] firstNames = { "Liam", "Emma", "Noah", "Olivia", "Ethan", "Sophia", "Mason", "Ava",
                                 "Logan", "Isabella", "Lucas", "Mia", "Aiden", "Charlotte", "Jackson" };
        string[] lastNames = { "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller",
                                 "Davis", "Wilson", "Taylor", "Anderson", "Thomas", "Martin", "White" };
        string[] cities = { "Toronto", "Vancouver", "Calgary", "Ottawa", "Edmonton",
                                 "Winnipeg", "Hamilton", "Quebec City", "Halifax", "Victoria" };
        string[] provinces = { "ON",      "BC",        "AB",      "ON",     "AB",
                                 "MB",      "ON",        "QC",      "NS",     "BC" };
        string[] streets = { "Maple Ave", "Oak St", "Cedar Rd", "Pine Blvd", "Birch Lane",
                                 "Elm Dr",   "Willow Way", "Spruce Cres", "Ash Ct", "Cherry St" };

        for (int i = 0; i < 15; i++)
        {
            var first = firstNames[rng.Next(firstNames.Length)];
            var last = lastNames[rng.Next(lastNames.Length)];
            var email = $"{first.ToLower()}.{last.ToLower()}{i}@home.com";

            // 1. Identity user
            var identityUser = await userManager.FindByEmailAsync(email);
            if (identityUser is null)
            {
                identityUser = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
                var result = await userManager.CreateAsync(identityUser, "Customer@2025!");
                if (!result.Succeeded) continue;
            }
            await userManager.AddToRoleAsync(identityUser, "Customer");

            // 2. RegisteredUser row
            if (await db.RegisteredUsers.AnyAsync(u => u.Email == email)) continue;

            var registeredUser = new RegisteredUserModel { Email = email };
            db.RegisteredUsers.Add(registeredUser);
            await db.SaveChangesAsync();

            // 3. Contact/address
            int cityIdx = rng.Next(cities.Length);
            var contact = new ContactDetailModel
            {
                FirstName = first,
                LastName = last,
                PhoneNumber = $"604-{rng.Next(100, 999)}-{rng.Next(1000, 9999)}",
                Street = $"{rng.Next(100, 9999)} {streets[rng.Next(streets.Length)]}",
                City = cities[cityIdx],
                Province = provinces[cityIdx],
                PostCode = $"{(char)('A' + rng.Next(26))}{rng.Next(1, 9)}{(char)('A' + rng.Next(26))} {rng.Next(1, 9)}{(char)('A' + rng.Next(26))}{rng.Next(1, 9)}",
                Country = "Canada",
                IsDefault = true,
                FkRegisteredUserId = registeredUser.PkRegisteredUserId
            };
            db.ContactDetails.Add(contact);
            await db.SaveChangesAsync();

            // 4. Orders (1-4 per customer)
            int orderCount = rng.Next(5, 15);
            for (int o = 0; o < orderCount; o++)
            {
                var orderDate = DateTime.UtcNow.AddDays(-rng.Next(10, 365));
                int roll = rng.Next(10);

                var (oStatus, dStatus) = roll switch
                {
                    < 6 => (OrderStatus.Shipped, DeliveryStatus.Delivered),
                    < 8 => (OrderStatus.Shipped, DeliveryStatus.Shipped),
                    < 9 => (OrderStatus.Pending, DeliveryStatus.InTransit),
                    _ => (OrderStatus.Pending, DeliveryStatus.Pending)
                };

                // Pick 1-3 products for this order
                int itemCount = rng.Next(1, 4);
                var pickedProd = products.OrderBy(_ => rng.Next()).Take(itemCount).ToList();
                decimal total = 0;

                var order = new OrderModel
                {
                    OrderStatus = oStatus,
                    DeliveryStatus = dStatus,
                    CreatedAt = orderDate,
                    TotalAmount = 0,           // updated below
                    FkRegisteredUserId = registeredUser.PkRegisteredUserId,
                    FkContactId = contact.PkContactId
                };
                db.Orders.Add(order);
                await db.SaveChangesAsync();

                foreach (var prod in pickedProd)
                {
                    int qty = rng.Next(1, 4);
                    decimal unit = prod.Price * (1 - prod.DiscountPercent / 100m);
                    total += unit * qty;

                    db.OrderItems.Add(new OrderItemModel
                    {
                        FkOrderId = order.PkOrderId,
                        FkProductId = prod.PkProductId,
                        Quantity = qty,
                        UnitPrice = unit
                    });
                }

                order.TotalAmount = total;
                await db.SaveChangesAsync();

                // 5. Transaction for completed orders
                if (oStatus == OrderStatus.Shipped)
                {
                    decimal deliveryFee = Math.Round((decimal)(rng.NextDouble() * 7 + 3), 2); // $3-$10
                    db.Transactions.Add(new TransactionModel
                    {
                        TransactionStatus = "Completed",
                        Amount = total + deliveryFee,
                        TransactionDate = orderDate.AddHours(rng.Next(1, 24)),
                        DeliveryFee = deliveryFee,
                        FkOrderId = order.PkOrderId,
                        FkContactId = contact.PkContactId
                    });
                    await db.SaveChangesAsync();
                }
            }
        }
    }

    // =========================================================================
    //  PRODUCT IMAGES
    //  Writes a ProductImageModel row (URL) into ApplicationDbContext and
    //  writes the binary into ImageStoreContext so both stay in sync.
    //
    //  Call pattern in Program.cs:
    //      await DbSeeder.SeedProductImagesAsync(appDb, imageDb, env.WebRootPath);
    //
    //  Place placeholder images under wwwroot/images/products/<slug>.png
    //  (or any extension) before running. The seeder will pick them up
    //  automatically. If no files exist the method is a no-op.
    // =========================================================================
    public static async Task SeedProductImagesAsync(
        ApplicationDbContext appDb,
        ImageStoreContext imageDb,
        string wwwRootPath)
    {
        var imagesFolder = Path.Combine(wwwRootPath, "images", "products");
        if (!Directory.Exists(imagesFolder)) return;

        var products = await appDb.Product
            .Include(p => p.ProductImage)
            .ToListAsync();

        foreach (var product in products)
        {
            // Skip products that already have at least one image URL recorded
            if (product.ProductImage != null && product.ProductImage.Any()) continue;

            // Try to find a matching file by normalised product name
            var slug = Normalize(product.Name).Replace(" ", "-");
            var extensions = new[] { ".png", ".jpg", ".jpeg", ".webp", ".gif" };

            string? filePath = null;
            foreach (var ext in extensions)
            {
                var candidate = Path.Combine(imagesFolder, slug + ext);
                if (File.Exists(candidate)) { filePath = candidate; break; }
            }

            // Fallback: any file that starts with the product PK
            if (filePath is null)
            {
                filePath = Directory
                    .EnumerateFiles(imagesFolder)
                    .FirstOrDefault(f => Path.GetFileNameWithoutExtension(f)
                                         .StartsWith(product.PkProductId.ToString()));
            }

            if (filePath is null) continue;   // no image found for this product, skip

            var fileName = Path.GetFileName(filePath);
            var ext2 = Path.GetExtension(filePath).TrimStart('.').ToLower();
            var mimeType = ext2 switch
            {
                "jpg" or "jpeg" => "image/jpeg",
                "gif" => "image/gif",
                "webp" => "image/webp",
                _ => "image/png"
            };

            // Relative URL served by the web server
            var relativeUrl = $"/images/products/{fileName}";

            // 1. ProductImageModel row in main DB
            appDb.ProductImage.Add(new ProductImageModel
            {
                FkProductId = product.PkProductId,
                ProductImageURL = relativeUrl
            });
            // 2. Binary copy in ImageStoreContext
            var imageBytes = await File.ReadAllBytesAsync(filePath);
            var alreadyInStore = await imageDb.Images
                .AnyAsync(img => img.FkProductId == product.PkProductId && img.FileName == fileName);

            if (!alreadyInStore)
            {
                imageDb.Images.Add(new ImageModel
                {
                    FileName = fileName,
                    Description = product.Name,
                    FileType = mimeType,
                    ImageData = imageBytes,
                    FkProductId = product.PkProductId,
                    ProductImageURL = relativeUrl
                });
            }
        }

        await appDb.SaveChangesAsync();
        await imageDb.SaveChangesAsync();
    }
}
