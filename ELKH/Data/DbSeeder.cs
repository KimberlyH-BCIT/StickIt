using ELKH.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace ELKH.Data;

/// <summary>
/// Seeds the database with demo categories and products on first run.
/// Entirely idempotent — skipped if any products already exist.
/// </summary>
public static class DbSeeder
{
    //public static async Task SeedTestTransactionsAsync(ApplicationDbContext db)
    //{
    //    if (await db.Orders.AnyAsync(o => o.OrderStatus == "Test")) return;

    //    // Assume at least one user, product, and contact exist
    //    var user = await db.RegisteredUsers.FirstOrDefaultAsync();
    //    var product = await db.Products.FirstOrDefaultAsync();
    //    var contact = await db.ContactDetails.FirstOrDefaultAsync();

    //    if (user == null || product == null || contact == null) return;

    //    var order = new OrderModel
    //    {
    //        OrderStatus = "Test",
    //        TotalAmount = product.Price,
    //        CreatedAt = DateTime.UtcNow,
    //        DeliveryStatus = "Pending",
    //        FkRegisteredUserId = user.PkRegisteredUserId,
    //        FkContactId = contact.PkContactId
    //    };
    //    db.Orders.Add(order);
    //    await db.SaveChangesAsync();

    //    var orderItem = new OrderItemModel
    //    {
    //        FkOrderId = order.PkOrderId,
    //        FkProductId = product.PkProductId,
    //        Quantity = 1
    //    };
    //    db.OrderItems.Add(orderItem);
    //    await db.SaveChangesAsync();

    //    var transaction = new TransactionModel
    //    {
    //        TransactionStatus = "Pending",
    //        Amount = product.Price,
    //        TransactionDate = DateTime.UtcNow,
    //        DeliveryFee = 5.99m,
    //        FkOrderId = order.PkOrderId,
    //        FkContactId = contact.PkContactId
    //    };
    //    db.Transactions.Add(transaction);

    //    var rating = new ProductRatingModel
    //    {
    //        FkProductId = product.PkProductId,
    //        FkRegisteredUserId = user.PkRegisteredUserId,
    //        FkOrderItemId = orderItem.PkOrderItemId,
    //        Rating = 5,
    //        Description = "Test review",
    //        RatedTime = DateTime.UtcNow,
    //        Approved = true
    //    };
    //    db.ProductRatings.Add(rating);

    //    await db.SaveChangesAsync();
    //}
    public static async Task SeedProductsAsync(ApplicationDbContext db)
    {
        // Skip if products already exist
        if (await db.Products.AnyAsync()) return;

        // ── 1. Ensure the seven categories exist ───────────────────────
        var categoryNames = new[]
        {
            "Die-Cut Stickers",
            "Holographic",
            "Waterproof",
            "Sheet Packs",
            "Anime & Pop Culture",
            "Nature & Floral",
            "Gaming"
        };

        foreach (var name in categoryNames)
        {
            if (!await db.Categories.AnyAsync(c => c.CategoryName == name))
                db.Categories.Add(new CategoryModel { CategoryName = name });
        }
        await db.SaveChangesAsync();

        // Load all seven categories with their assigned PKs
        var cats = await db.Categories
            .Where(c => categoryNames.Contains(c.CategoryName))
            .ToDictionaryAsync(c => c.CategoryName);

        var die    = cats["Die-Cut Stickers"];
        var holo   = cats["Holographic"];
        var water  = cats["Waterproof"];
        var sheet  = cats["Sheet Packs"];
        var anime  = cats["Anime & Pop Culture"];
        var nature = cats["Nature & Floral"];
        var gaming = cats["Gaming"];

        // ── 2. Build the 40 products ────────────────────────────────────
        var products = new List<ProductModel>
        {
            // ── Die-Cut Stickers (8) ────────────────────────────────────
            P("Kawaii Cat Die-Cut Sticker",
              "Adorable kawaii-style cat with big sparkle eyes. Printed on premium vinyl with a glossy finish.",
              2.99m, 0, 150, die),

            P("Shiba Inu Doge Die-Cut Sticker",
              "Classic internet-icon Shiba Inu in die-cut form. Very sticker. Much wow.",
              2.49m, 0, 200,  die),

            P("Cactus Friends Die-Cut Sticker",
              "A cheerful cactus trio perfect for laptops, water bottles, and planners.",
              1.99m, 10, 175,  die),

            P("Vintage Camera Die-Cut Sticker",
              "Retro 35 mm film camera rendered in warm pastel tones. Great for photographers.",
              3.49m, 0, 120, die),

            P("Astronaut Floating Die-Cut Sticker",
              "A tiny astronaut drifting through a pastel galaxy. Approx. 7 cm tall.",
              2.99m, 0, 160,die),

            P("Boba Tea Die-Cut Sticker",
              "Brown sugar milk tea with tapioca pearls. UV-resistant ink keeps colours vivid.",
              2.49m, 15, 180, die),

            P("Avocado Toast Die-Cut Sticker",
              "Trendy avocado toast slice with a smiling face. Dishwasher-safe laminate.",
              1.99m, 0, 140,die),

            P("Retro Cassette Die-Cut Sticker",
              "80s-style audio cassette tape in neon colours. Perfect for notebooks and guitar cases.",
              2.99m, 0, 130,die),

            // ── Holographic (6) ─────────────────────────────────────────
            P("Rainbow Galaxy Holographic Sticker",
              "Shifts through the full visible spectrum in direct light. Deep-space galaxy artwork.",
              3.99m, 0, 100, holo),

            P("Unicorn Horn Holographic Sticker",
              "Prismatic spiral unicorn horn that sparkles with every movement.",
              3.49m, 20, 90, holo),

            P("Crystal Prism Holographic Sticker",
              "Geometric crystal prism design with metallic rainbow shimmer. 8 cm wide.",
              4.99m, 0, 75, holo),

            P("Shooting Star Holographic Sticker",
              "A streaking shooting star with a long rainbow tail. Make a wish!",
              3.99m, 0, 95, holo),

            P("Northern Lights Holographic Sticker",
              "Aurora borealis waves captured in a colour-shifting holographic print.",
              4.49m, 10, 80,holo ),

            P("Butterfly Wings Holographic Sticker",
              "Iridescent butterfly wings that appear to move when tilted. 9 cm wingspan.",
              3.99m, 0, 110,holo),

            // ── Waterproof (6) ──────────────────────────────────────────
            P("Ocean Waves Waterproof Sticker",
              "Japanese woodblock-inspired wave design. Fully waterproof and scratch-resistant.",
              3.49m, 0, 120, water),

            P("Mountain Peak Waterproof Sticker",
              "Minimalist mountain range silhouette in cool blues. Survives dishwashers and rain.",
              3.99m, 0, 110, water),

            P("City Skyline Waterproof Sticker",
              "Generic modern cityscape at dusk. Weatherproof vinyl with UV coating.",
              4.49m, 0, 85,water),

            P("Compass Rose Waterproof Sticker",
              "Vintage nautical compass rose in antique gold. Great for adventure gear.",
              3.99m, 15, 95,water),

            P("Deep Sea Fish Waterproof Sticker",
              "Bioluminescent anglerfish glowing in the deep ocean dark. Waterproof UV ink.",
              3.49m, 0, 130, water),

            P("Sunset Palm Waterproof Sticker",
              "Tropical palm silhouette against a gradient sunset. Fade-resistant outdoor vinyl.",
              3.99m, 0, 100,water),

            // ── Sheet Packs (5) ─────────────────────────────────────────
            P("Cottagecore Sheet Pack (20 Stickers)",
              "Mushrooms, flowers, hedgehogs, and vintage teapots — 20 illustrated stickers on one sheet.",
              8.99m, 0, 60, sheet),

            P("Space Explorer Sheet Pack (16 Stickers)",
              "Rockets, planets, astronauts, and satellites. 16 stickers for the stargazer in you.",
              7.99m, 10, 55, sheet),

            P("Retro Vibes Sheet Pack (24 Stickers)",
              "Cassettes, boom boxes, pixel art, and neon signs. 24 stickers for maximum nostalgia.",
              10.99m, 0, 45, sheet),

            P("Cute Animals Sheet Pack (20 Stickers)",
              "20 chibi-style animals including pandas, foxes, frogs, and capybaras.",
              8.99m, 25, 70, sheet),

            P("Fantasy Creatures Sheet Pack (18 Stickers)",
              "Dragons, phoenixes, mermaids, and griffins across 18 hand-drawn stickers.",
              9.49m, 0, 50, sheet),

            // ── Anime & Pop Culture (6) ─────────────────────────────────
            P("Totoro Forest Spirit Sticker",
              "Fan art-inspired forest spirit from the beloved Studio Ghibli universe. 6 cm tall.",
              2.99m, 0, 160, anime),

            P("Pikachu Chibi Sticker",
              "Classic yellow electric mouse in super-deformed chibi style. 5 cm tall.",
              2.49m, 0, 200, anime),

            P("Scout Regiment Sticker",
              "Wings of Freedom emblem from the hit manga series. Matte laminate finish.",
              3.49m, 10, 140, anime),

            P("Jolly Roger Sticker",
              "Skull-and-crossbones pirate flag from the beloved grand-line adventure series.",
              2.99m, 0, 150, anime),

            P("Energy Blast Sticker",
              "Iconic blue power ball inspired by the legendary anime power level sequence.",
              3.49m, 0, 130, anime),

            P("Leaf Village Symbol Sticker",
              "Hidden leaf village crest from the world-famous ninja academy series.",
              2.99m, 20, 145, anime),

            // ── Nature & Floral (5) ─────────────────────────────────────
            P("Cherry Blossom Branch Sticker",
              "Delicate sakura branch in soft pinks. Botanical illustration style, 10 cm wide.",
              2.49m, 0, 170, nature),

            P("Wildflower Meadow Sticker",
              "Loose watercolour wildflowers — daisies, lavender, poppies — on a white field.",
              3.49m, 0, 120,nature),

            P("Autumn Leaves Sticker",
              "Four falling autumn leaves in crimson, amber, rust, and gold.",
              2.99m, 0, 155, nature),

            P("Succulent Garden Sticker",
              "A pot of assorted succulents rendered in a clean, modern illustration style.",
              3.49m, 15, 110, nature),

            P("Monstera Leaf Sticker",
              "Tropical monstera deliciosa leaf in deep green with natural splits. 9 cm tall.",
              2.99m, 0, 160, nature),

            // ── Gaming (4) ──────────────────────────────────────────────
            P("Retro Game Controller Sticker",
              "Classic D-pad and button controller rendered in pixel art. For all the old-school gamers.",
              3.49m, 0, 140, gaming),

            P("Pixel Sword & Shield Sticker",
              "8-bit fantasy weapon set — longsword and kite shield in silver and blue pixels.",
              2.99m, 10, 120,gaming),

            P("Game Over Screen Sticker",
              "Retro arcade GAME OVER text on a black background with glowing red letters.",
              2.49m, 0, 130, gaming),

            P("Boss Fight Dragon Sticker",
              "Epic pixel-art red dragon in full battle stance. The ultimate boss encounter.",
              3.99m, 0, 95, gaming),
        };

        db.Products.AddRange(products);
        await db.SaveChangesAsync();
    }

    // ── Helper ──────────────────────────────────────────────────────────
    /// <summary>
    /// Shorthand factory method for creating a seed <see cref="ProductModel"/>.
    /// The single-letter name keeps the inline product table above readable.
    /// <see cref="ProductModel.NameNormalized"/> is populated via <see cref="Normalize"/>
    /// so every seeded product is immediately searchable without a separate reindex pass.
    /// </summary>
    private static ProductModel P(
        string name,
        string description,
        decimal price,
        decimal discountPercent,
        int stock,
        CategoryModel category) => new()
    {
        Name             = name,
        NameNormalized   = Normalize(name),
        Description      = description,
        Price            = price,
        DiscountPercent  = discountPercent,
        StockQuantity    = stock,
        FkCategoryId     = category.PkCategoryId,
        Category         = category
    };

    /// <summary>
    /// Returns a lowercase, diacritic-free copy of <paramref name="name"/> for use as
    /// <see cref="ProductModel.NameNormalized"/> during seeding.
    /// Identical in behaviour to <c>ProductService.NormalizeName()</c>: NFD decomposition
    /// strips combining marks (diacritics), NFC re-composition reassembles the result,
    /// and <c>ToLowerInvariant</c> applies culture-independent case folding.
    /// Both methods must stay in sync if the normalization strategy ever changes.
    /// </summary>
    private static string Normalize(string name)
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

    // ── Admin Seeder ─────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds a default Admin role and a single admin test account.
    /// Idempotent — skipped if the role or account already exist.
    ///
    /// Credentials are resolved from <paramref name="configuration"/> using the keys
    /// <c>Seed:AdminEmail</c> and <c>Seed:AdminPass</c>.
    /// Set these via <c>dotnet user-secrets</c> in development and via environment
    /// variables or Azure Key Vault in production. Never commit real credentials.
    /// </summary>
    public static async Task SeedAdminAsync(
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration)
    {
        const string adminRole    = "Admin";
        const string customerRole = "Customer";

        // Read from user-secrets / environment variables.
        // The fallback values are intentionally weak dev-only defaults;
        // they must be overridden for any shared or internet-accessible environment.
        var adminEmail = configuration["Seed:AdminEmail"] ?? "admin@stickit.dev";
        var adminPass  = configuration["Seed:AdminPass"]  ?? "Admin@2025!";

        // Ensure both roles exist regardless of whether the users already exist.
        if (!await roleManager.RoleExistsAsync(adminRole))
            await roleManager.CreateAsync(new IdentityRole(adminRole));

        if (!await roleManager.RoleExistsAsync(customerRole))
            await roleManager.CreateAsync(new IdentityRole(customerRole));

        var admin = await userManager.FindByEmailAsync(adminEmail);

        if (admin is null)
        {
            admin = new IdentityUser
            {
                UserName       = adminEmail,
                Email          = adminEmail,
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(admin, adminPass);
            if (!result.Succeeded) return;
        }

        // Always verify the role assignment — covers the case where the user
        // was created on a previous run but role assignment failed.
        if (!await userManager.IsInRoleAsync(admin, adminRole))
            await userManager.AddToRoleAsync(admin, adminRole);
    }

    // ── Customer Seeder ──────────────────────────────────────────────────────

    /// <summary>
    /// Seeds 50 demo customer accounts with contact details, order histories,
    /// wishlists, and reviews. Idempotent — skipped if any @home.com users exist.
    /// </summary>
    public static async Task SeedCustomersAsync(
        ApplicationDbContext db,
        UserManager<IdentityUser> userManager,
        string wwwRootPath)
    {
        if (await db.RegisteredUsers.AnyAsync(u => u.Email.EndsWith("@home.com")))
            return;

        // Load the shared placeholder avatar once.
        var avatarPath = Path.Combine(wwwRootPath, "images", "placeholder.png");
        byte[]? avatarBytes = File.Exists(avatarPath)
            ? await File.ReadAllBytesAsync(avatarPath)
            : null;

        var products = await db.Products.AsNoTracking().ToListAsync();
        if (products.Count == 0) return;

        var rng = new Random(42);

        // ── Data pools ───────────────────────────────────────────────────────
        string[] firstNames =
        [
            "Emma","Liam","Olivia","Noah","Ava","Ethan","Sophia","Lucas","Isabella","Mason",
            "Mia","Oliver","Charlotte","Elijah","Amelia","James","Harper","Aiden","Evelyn",
            "Alexander","Abigail","Sebastian","Emily","Michael","Elizabeth","Owen","Sofia",
            "Carter","Chloe","Jackson","Luna","Daniel","Penelope","Mateo","Riley","Henry",
            "Zoey","Jack","Nora","Logan","Lily","Dylan","Eleanor","Ryan","Hannah","Nathan",
            "Lillian","Tyler","Addison","Aaron"
        ];

        string[] lastNames =
        [
            "Smith","Johnson","Brown","Taylor","Wilson","Davis","Anderson","Martinez","Thomas",
            "Jackson","White","Harris","Thompson","Garcia","Moore","Robinson","Clark","Rodriguez",
            "Lewis","Lee","Walker","Hall","Young","Allen","King","Wright","Scott","Green","Baker",
            "Adams","Nelson","Carter","Mitchell","Perez","Roberts","Turner","Phillips","Campbell",
            "Parker","Evans","Edwards","Collins","Stewart","Sanchez","Morris","Rogers","Reed",
            "Cook","Morgan","Bell"
        ];

        // city, province, postal prefix
        (string City, string Province, char Prefix)[] locations =
        [
            ("Toronto",            "Ontario",                      'M'),
            ("Ottawa",             "Ontario",                      'K'),
            ("Hamilton",           "Ontario",                      'L'),
            ("London",             "Ontario",                      'N'),
            ("Kingston",           "Ontario",                      'K'),
            ("Montreal",           "Quebec",                       'H'),
            ("Quebec City",        "Quebec",                       'G'),
            ("Laval",              "Quebec",                       'H'),
            ("Vancouver",          "British Columbia",             'V'),
            ("Victoria",           "British Columbia",             'V'),
            ("Surrey",             "British Columbia",             'V'),
            ("Calgary",            "Alberta",                      'T'),
            ("Edmonton",           "Alberta",                      'T'),
            ("Winnipeg",           "Manitoba",                     'R'),
            ("Saskatoon",          "Saskatchewan",                 'S'),
            ("Regina",             "Saskatchewan",                 'S'),
            ("Halifax",            "Nova Scotia",                  'B'),
            ("Moncton",            "New Brunswick",                'E'),
            ("Charlottetown",      "Prince Edward Island",         'C'),
            ("St. John's",         "Newfoundland and Labrador",    'A'),
        ];

        string[] streetSuffixes = ["St","Ave","Rd","Blvd","Cres","Dr","Way","Lane","Pl","Ct"];
        string[] streetNames    =
        [
            "Maple","Oak","Pine","Cedar","Elm","Birch","Walnut","Willow","Spruce","Ash",
            "Poplar","Cherry","Larch","Fir","Sycamore","Hazel","Beech","Alder","Rowan","Hawthorn"
        ];

        string[] reviewTexts =
        [
            "Love this sticker! Great quality and arrived quickly.",
            "Really happy with this purchase. The colours are vibrant.",
            "Perfect for my laptop. Very sticky and durable.",
            "Excellent product! Better than the photos. Will buy again.",
            "Great value for the price. Highly recommend.",
            "Peels cleanly without residue. Very impressed.",
            "Cute design! My kids loved it too.",
            "Solid quality, holds up well on my water bottle.",
            "Super fast shipping and exactly as described.",
            "Would give 6 stars if I could. Absolutely amazing.",
            "Sharp print and the colours really pop.",
            "Nice sticker, slightly smaller than expected but still love it.",
            "Exactly what I was looking for. Great for my planner.",
            "The holographic effect is stunning in direct light!",
            "Waterproof as advertised — survived a full wash cycle.",
            "Decent quality for the price. Would order again.",
            "Beautiful design. Sticks perfectly and looks great.",
            "Good product but took a while to arrive.",
            "",  // star-only (no comment)
            "",  // star-only (no comment)
        ];

        // Weighted star ratings: skewed toward 4–5 stars for realistic data.
        int[] starPool = [1, 2, 3, 3, 4, 4, 4, 5, 5, 5];

        // ── Create 50 customers ──────────────────────────────────────────────
        for (int i = 0; i < 50; i++)
        {
            var firstName = firstNames[i];
            var lastName  = lastNames[i];
            var email     = $"{firstName.ToLowerInvariant()}.{lastName.ToLowerInvariant()}{i + 1}@home.com";
            var password  = $"Demo@2025!{(i + 1):D2}";

            // 1. Identity user (email pre-confirmed so the account is usable immediately).
            var identityUser = new IdentityUser
            {
                UserName       = email,
                Email          = email,
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(identityUser, password);
            if (!result.Succeeded) continue;

            // 2. RegisteredUserModel
            var registeredUser = new RegisteredUserModel { Email = email };
            db.RegisteredUsers.Add(registeredUser);
            await db.SaveChangesAsync();

            // 3. UserProfileModel — placeholder avatar
            db.UserProfiles.Add(new UserProfileModel
            {
                PkEmail        = email,
                FirstName      = firstName,
                LastName       = lastName,
                AvatarData     = avatarBytes,
                AvatarMimeType = avatarBytes is not null ? "image/png" : null
            });

            // 4. Default contact / shipping address
            var loc        = locations[rng.Next(locations.Length)];
            var streetNum  = rng.Next(1, 9999);
            var streetName = streetNames[rng.Next(streetNames.Length)];
            var streetSfx  = streetSuffixes[rng.Next(streetSuffixes.Length)];
            // Canadian postal code format: A1A 1A1
            // loc.Prefix supplies the FSA letter (first character, region-specific).
            // Each digit is rng.Next(1,9) and each extra letter is (char)('A' + rng.Next(26)).
            var postalCode = $"{loc.Prefix}{rng.Next(1, 9)}{(char)('A' + rng.Next(26))} {rng.Next(1, 9)}{(char)('A' + rng.Next(26))}{rng.Next(1, 9)}";

            var contact = new ContactDetailModel
            {
                FirstName          = firstName,
                LastName           = lastName,
                PhoneNumber        = $"({rng.Next(200, 999)}) {rng.Next(100, 999)}-{rng.Next(1000, 9999)}",
                Street             = $"{streetNum} {streetName} {streetSfx}",
                City               = loc.City,
                Province           = loc.Province,
                PostCode           = postalCode,
                Country            = "Canada",
                IsDefault          = true,
                FkRegisteredUserId = registeredUser.PkRegisteredUserId
            };
            db.ContactDetails.Add(contact);
            await db.SaveChangesAsync();

            // 5. Wishlist
            var wishlist = new WishListModel { FkUserId = registeredUser.PkRegisteredUserId };
            db.WishLists.Add(wishlist);
            await db.SaveChangesAsync();

            // 6. Wishlist items — 2 to 4 random products
            var wishlistProducts = products.OrderBy(_ => rng.Next()).Take(rng.Next(2, 5)).ToList();
            foreach (var wp in wishlistProducts)
            {
                db.WishListItems.Add(new WishListItemModel
                {
                    FkWishListId = wishlist.PkWishListId,
                    FkProductId  = wp.PkProductId,
                    DateAdded    = DateTime.UtcNow.AddDays(-rng.Next(1, 180))
                });
            }

            // 7. Orders — 1 to 3 per customer
            int orderCount = rng.Next(1, 4);
            for (int o = 0; o < orderCount; o++)
            {
                var orderDate = DateTime.UtcNow.AddDays(-rng.Next(15, 400));

                // Weight order statuses: ~40 % delivered, ~30 % shipped, ~20 % processing, ~10 % pending
                int roll = rng.Next(10);
                var (orderStatus, deliveryStatus) = roll switch
                {
                    < 4 => ("Delivered",  "Delivered"),
                    < 7 => ("Shipped",    "Shipped"),
                    < 9 => ("Processing", "In Transit"),
                    _   => ("Pending",    "Pending")
                };

                var orderProducts = products.OrderBy(_ => rng.Next()).Take(rng.Next(1, 4)).ToList();

                var order = new OrderModel
                {
                    OrderStatus        = orderStatus,
                    TotalAmount        = 0,
                    CreatedAt          = orderDate,
                    DeliveryStatus     = deliveryStatus,
                    FkRegisteredUserId = registeredUser.PkRegisteredUserId,
                    FkContactId        = contact.PkContactId
                };
                db.Orders.Add(order);
                await db.SaveChangesAsync();

                decimal orderTotal = 0;
                var orderItems = new List<OrderItemModel>();
                foreach (var prod in orderProducts)
                {
                    int qty = rng.Next(1, 3);
                    decimal effectivePrice = prod.DiscountPercent > 0
                        ? prod.Price * (1 - prod.DiscountPercent / 100m)
                        : prod.Price;
                    orderTotal += effectivePrice * qty;

                    var item = new OrderItemModel
                    {
                        FkOrderId   = order.PkOrderId,
                        FkProductId = prod.PkProductId,
                        Quantity    = qty
                    };
                    db.OrderItems.Add(item);
                    orderItems.Add(item);
                }
                await db.SaveChangesAsync();

                order.TotalAmount = Math.Round(orderTotal, 2);

                // Transaction for fulfilled orders
                if (orderStatus is "Delivered" or "Shipped")
                {
                    db.Transactions.Add(new TransactionModel
                    {
                        TransactionStatus = "Completed",
                        Amount            = Math.Round(orderTotal + 5.99m, 2),
                        TransactionDate   = orderDate.AddMinutes(rng.Next(5, 90)),
                        DeliveryFee       = 5.99m,
                        FkOrderId         = order.PkOrderId,
                        FkContactId       = contact.PkContactId
                    });
                }
                await db.SaveChangesAsync();

                // 8. Review for one item from delivered/shipped orders (~66 % chance per order)
                if (orderStatus is "Delivered" or "Shipped" && rng.Next(3) > 0)
                {
                    var ratedItem   = orderItems[rng.Next(orderItems.Count)];
                    var reviewText  = reviewTexts[rng.Next(reviewTexts.Length)];
                    var starRating  = starPool[rng.Next(starPool.Length)];
                    var reviewDate  = orderDate.AddDays(rng.Next(2, 21));

                    // Guard: one rating per order item per user
                    bool alreadyRated = await db.ProductRatings.AnyAsync(r =>
                        r.FkOrderItemId      == ratedItem.PkOrderItemId &&
                        r.FkRegisteredUserId == registeredUser.PkRegisteredUserId);

                    if (!alreadyRated)
                    {
                        db.ProductRatings.Add(new ProductRatingModel
                        {
                            FkProductId        = ratedItem.FkProductId,
                            FkRegisteredUserId = registeredUser.PkRegisteredUserId,
                            FkOrderItemId      = ratedItem.PkOrderItemId,
                            Rating             = starRating,
                            Description        = reviewText,
                            RatedTime          = reviewDate,
                            Approved           = true,
                            IsFlagged          = false
                        });
                        await db.SaveChangesAsync();
                    }
                }
            }

            await db.SaveChangesAsync();
        }
    }
}
