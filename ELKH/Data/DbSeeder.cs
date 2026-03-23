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
public static partial class DbSeeder
{
    public static async Task SeedProductsAsync(ApplicationDbContext db)
    {
        // Skip if products already exist
        if (await db.Products.AnyAsync()) return;

        // ── 1. Ensure the eleven categories exist ──────────────────────
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

        // Load all eleven categories with their assigned PKs
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

        // ── 2. Build the 416 products ────────────────────────────────────
        var products = GetProducts(canadian, christmas, animals, easter, food, 
                                    halloween, lunarNY, nature, newYear, thanks, misc);

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
        CategoryModel category,
        string tags = "") => new()
    {
        Name             = name,
        NameNormalized   = Normalize(name),
        Description      = description,
        Price            = price,
        DiscountPercent  = discountPercent,
        StockQuantity    = stock,
        IsActive         = true,
        FkCategoryId     = category.PkCategoryId,
        Category         = category,
        Tags             = tags
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
    /// Seeds default Admin, Manager, and Staff roles with corresponding test accounts.
    /// Also ensures the Customer role exists for registration.
    /// Idempotent — skipped if roles or accounts already exist.
    ///
    /// Credentials are resolved from <paramref name="configuration"/> using the keys
    /// <c>Seed:AdminEmail</c>, <c>Seed:AdminPass</c>, <c>Seed:ManagerEmail</c>, 
    /// <c>Seed:ManagerPass</c>, <c>Seed:StaffEmail</c>, and <c>Seed:StaffPass</c>.
    /// Set these via <c>dotnet user-secrets</c> in development and via environment
    /// variables or Azure Key Vault in production. Never commit real credentials.
    /// </summary>
    public static async Task SeedAdminAsync(
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration)
    {
        const string adminRole    = "Admin";
        const string managerRole  = "Manager";
        const string staffRole    = "Staff";
        const string customerRole = "Customer";

        // Read from user-secrets / environment variables.
        // The fallback values are intentionally weak dev-only defaults;
        // they must be overridden for any shared or internet-accessible environment.
        // IsNullOrWhiteSpace guards against the empty-string values in appsettings.json,
        // which would never satisfy the ?? operator and would create a credential-less account.
        var adminEmail = configuration["Seed:AdminEmail"];
        if (string.IsNullOrWhiteSpace(adminEmail)) adminEmail = "admin@stickit.dev";
        var adminPass  = configuration["Seed:AdminPass"];
        if (string.IsNullOrWhiteSpace(adminPass))  adminPass  = "Admin@2025!";

        var managerEmail = configuration["Seed:ManagerEmail"];
        if (string.IsNullOrWhiteSpace(managerEmail)) managerEmail = "manager@stickit.dev";
        var managerPass  = configuration["Seed:ManagerPass"];
        if (string.IsNullOrWhiteSpace(managerPass))  managerPass  = "Manager@2025!";

        var staffEmail = configuration["Seed:StaffEmail"];
        if (string.IsNullOrWhiteSpace(staffEmail)) staffEmail = "staff@stickit.dev";
        var staffPass  = configuration["Seed:StaffPass"];
        if (string.IsNullOrWhiteSpace(staffPass))  staffPass  = "Staff@2025!";

        // Ensure all four roles exist regardless of whether the users already exist.
        foreach (var role in new[] { adminRole, managerRole, staffRole, customerRole })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // ── Seed Admin Account ───────────────────────────────────────────────
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

        // ── Seed Manager Account ─────────────────────────────────────────────
        var manager = await userManager.FindByEmailAsync(managerEmail);

        if (manager is null)
        {
            manager = new IdentityUser
            {
                UserName       = managerEmail,
                Email          = managerEmail,
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(manager, managerPass);
            if (result.Succeeded && !await userManager.IsInRoleAsync(manager, managerRole))
                await userManager.AddToRoleAsync(manager, managerRole);
        }
        else if (!await userManager.IsInRoleAsync(manager, managerRole))
        {
            await userManager.AddToRoleAsync(manager, managerRole);
        }

        // ── Seed Staff Account ───────────────────────────────────────────────
        var staff = await userManager.FindByEmailAsync(staffEmail);

        if (staff is null)
        {
            staff = new IdentityUser
            {
                UserName       = staffEmail,
                Email          = staffEmail,
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(staff, staffPass);
            if (result.Succeeded && !await userManager.IsInRoleAsync(staff, staffRole))
                await userManager.AddToRoleAsync(staff, staffRole);
        }
        else if (!await userManager.IsInRoleAsync(staff, staffRole))
        {
            await userManager.AddToRoleAsync(staff, staffRole);
        }
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

            // Assign Customer role to all seeded customer accounts
            await userManager.AddToRoleAsync(identityUser, "Customer");

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
