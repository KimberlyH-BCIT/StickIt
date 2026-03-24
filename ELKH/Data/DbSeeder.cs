using ELKH.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace ELKH.Data;

/*
 * ┌────────────────────────────────────────────────────────────────────────────┐
 * │ TABLE OF CONTENTS - DbSeeder.cs                                            │
 * ├────────────────────────────────────────────────────────────────────────────┤
 * │ 1. Product Seeding ..................................... Lines  20-116      │
 * │    - SeedProductsAsync: 416 products across 11 categories                  │
 * │    - GetProducts: Defined in DbSeeder.Products.cs (partial class)          │
 * │    - Idempotency: Skipped if any products exist                            │
 * │                                                                            │
 * │ 2. Admin & Role Seeding ............................... Lines 118-226      │
 * │    - SeedAdminAsync: Create Admin/Manager/Staff/Customer roles             │
 * │    - Seed 3 test accounts with credentials from user-secrets               │
 * │    - Idempotency: Skipped if roles/users exist                             │
 * │                                                                            │
 * │ 3. Customer & Order Seeding ........................... Lines 228-460+     │
 * │    - SeedCustomersAsync: 50 demo customers                                 │
 * │    - Contact details with realistic Canadian addresses                     │
 * │    - Order histories (1-3 orders per customer)                             │
 * │    - Wishlists (2-4 products per customer)                                 │
 * │    - Reviews with weighted ratings (skewed toward 4-5 stars)               │
 * │    - Idempotency: Skipped if @home.com users exist                         │
 * │                                                                            │
 * │ 4. Helper Methods ..................................... Lines  69-115      │
 * │    - P(): Shorthand factory for ProductModel                               │
 * │    - Normalize(): Diacritic removal for search (synced with ProductService)│
 * └────────────────────────────────────────────────────────────────────────────┘
 */

/// <summary>
/// Database seeding orchestration for the ELKH e-commerce application.
/// Provides three main seeding methods for products, administrative accounts, and demo customers.
///
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
/// <para><strong>Partial Class Design:</strong></para>
/// The <c>GetProducts()</c> method is defined in <c>DbSeeder.Products.cs</c> to keep
/// the 416-product catalog separated from seeding orchestration logic.
/// </summary>
/// <remarks>
/// Call these methods in <c>Program.cs</c> during application startup:
/// <code>
/// await DbSeeder.SeedProductsAsync(db);
/// await DbSeeder.SeedAdminAsync(userManager, roleManager, config);
/// await DbSeeder.SeedCustomersAsync(db, userManager, wwwroot);
/// </code>
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

    #region Helper Methods

    // Random number generator for dates and bestseller/trending flags
    private static readonly Random _random = new Random(42); // Fixed seed for reproducible results

    // ────────────────────────────────────────────────────────────────────────
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
        FkCategoryId     = category.PkCategoryId,
        Category         = category,
        Tags             = tags,
        DateAdded        = GenerateRandomDate(),
        IsBestSeller     = isBestSeller,
        IsTrending       = isTrending
    };

    /// <summary>
    /// Generates a random date between 2 years ago and now, with higher probability
    /// of recent dates (weighted toward the past 6 months).
    /// </summary>
    private static DateTime GenerateRandomDate()
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

    #endregion

    #region Admin & Role Seeding

    /// <summary>
    /// Seeds default Admin, Manager, Staff, and Customer roles with corresponding test accounts.
    /// Fully idempotent—skipped if roles or accounts already exist.
    /// </summary>
    /// <param name="userManager">ASP.NET Core Identity UserManager for account creation.</param>
    /// <param name="roleManager">ASP.NET Core Identity RoleManager for role creation.</param>
    /// <param name="configuration">
    /// Application configuration for retrieving credentials. Reads the following keys:
    /// <list type="bullet">
    /// <item><c>Seed:AdminEmail</c> and <c>Seed:AdminPass</c></item>
    /// <item><c>Seed:ManagerEmail</c> and <c>Seed:ManagerPass</c></item>
    /// <item><c>Seed:StaffEmail</c> and <c>Seed:StaffPass</c></item>
    /// </list>
    /// </param>
    /// <remarks>
    /// <para><strong>⚠️ SECURITY WARNING:</strong></para>
    /// Credentials must be configured via <c>dotnet user-secrets</c> in development
    /// or environment variables/Azure Key Vault in production. The fallback defaults
    /// (admin@stickit.dev / Admin@2025!) are intentionally weak and suitable ONLY for
    /// local development. Never deploy with default credentials.
    ///
    /// <para><strong>Idempotency Strategy:</strong></para>
    /// <list type="number">
    /// <item>Create roles if they don't exist (always safe to call)</item>
    /// <item>Create user accounts if they don't exist</item>
    /// <item>Always verify and fix role assignments (handles partial failures)</item>
    /// </list>
    ///
    /// <para><strong>Role Hierarchy:</strong></para>
    /// <list type="bullet">
    /// <item><strong>Admin</strong>: Full system access (user management, sales reports, cache control)</item>
    /// <item><strong>Manager</strong>: Order management, transaction viewing, inventory oversight</item>
    /// <item><strong>Staff</strong>: Order fulfillment and customer support</item>
    /// <item><strong>Customer</strong>: Shopping and account management (assigned during registration)</item>
    /// </list>
    /// </remarks>
    public static async Task SeedAdminAsync(
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration)
    {
        const string adminRole    = "Admin";
        const string managerRole  = "Manager";
        const string staffRole    = "Staff";
        const string customerRole = "Customer";

        // ══════════════════════════════════════════════════════════════════════
        // ║ Load Credentials from Configuration                                ║
        // ║ Fallback to weak defaults only for local development.              ║
        // ══════════════════════════════════════════════════════════════════════
        // Read from user-secrets / environment variables.
        // IsNullOrWhiteSpace guards against empty-string values in appsettings.json,
        // which would bypass the ?? operator and create credential-less accounts.
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

        // ══════════════════════════════════════════════════════════════════════
        // ║ Ensure Roles Exist                                                 ║
        // ║ Create all four roles regardless of whether users exist.           ║
        // ══════════════════════════════════════════════════════════════════════
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

    #endregion

    #region Customer & Order Seeding

    // ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds 50 demo customer accounts with realistic profiles, contact details,
    /// order histories, wishlists, and product reviews.
    /// Fully idempotent—skipped if any @home.com users already exist.
    /// </summary>
    /// <param name="db">Database context for creating customer-related entities.</param>
    /// <param name="userManager">ASP.NET Core Identity UserManager for creating customer accounts.</param>
    /// <param name="wwwRootPath">
    /// Web root path for loading the shared placeholder avatar (images/placeholder.png).
    /// </param>
    /// <remarks>
    /// <para><strong>Generated Data Includes:</strong></para>
    /// <list type="bullet">
    /// <item><strong>Accounts:</strong> 50 IdentityUser + RegisteredUserModel + UserProfileModel</item>
    /// <item><strong>Credentials:</strong> Email format: firstname.lastnameN@home.com, Password: Demo@2025!##</item>
    /// <item><strong>Contact Details:</strong> Realistic Canadian addresses with proper postal codes</item>
    /// <item><strong>Wishlists:</strong> 2-4 random products per customer</item>
    /// <item><strong>Orders:</strong> 1-3 orders per customer with realistic status distribution</item>
    /// <item><strong>Reviews:</strong> Weighted ratings skewed toward 4-5 stars</item>
    /// </list>
    ///
    /// <para><strong>Data Pools:</strong></para>
    /// Uses predefined arrays of first names, last names, Canadian cities/provinces,
    /// street names, and review text templates for realistic variation.
    ///
    /// <para><strong>Postal Code Generation:</strong></para>
    /// Canadian format (A1A 1A1) with region-appropriate FSA prefixes:
    /// M/K/L/N (Ontario), H/G (Quebec), V (BC), T (Alberta), R (Manitoba), S (Saskatchewan),
    /// B (Nova Scotia), E (New Brunswick), C (PEI), A (Newfoundland).
    ///
    /// <para><strong>Order Status Distribution (Weighted):</strong></para>
    /// 40% Delivered, 30% Shipped, 20% Processing, 10% Pending.
    ///
    /// <para><strong>Review Rating Distribution (Weighted):</strong></para>
    /// Skewed toward 4-5 stars using weighted pool [1, 2, 3, 3, 4, 4, 4, 5, 5, 5].
    /// </remarks>
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
            // 5-star reviews (excellent)
            "Absolutely love this sticker! Great quality and arrived quickly.",
            "Really happy with this purchase. The colours are vibrant and true to the image.",
            "Perfect for my laptop. Very sticky and durable - survived daily use perfectly.",
            "Excellent product! Better than the photos. Will definitely buy again.",
            "Outstanding quality for the price. Highly recommend to everyone!",
            "Peels cleanly without residue. Very impressed with the adhesive technology.",
            "Cute design! My kids loved it too - perfect family-friendly sticker.",
            "Solid quality, holds up well on my water bottle through many washes.",
            "Super fast shipping and exactly as described. Top-notch service!",
            "Would give 6 stars if I could. Absolutely amazing artwork and quality.",
            "Sharp print quality and the colours really pop. Professional-grade.",
            "Exactly what I was looking for. Great addition to my planner collection.",
            "The holographic effect is stunning in direct light! Eye-catching design.",
            "Waterproof as advertised — survived a full wash cycle without damage.",
            "Beautiful design. Sticks perfectly and looks fantastic on my car window.",
            "Perfect size and amazing detail. This exceeded my expectations completely.",
            "Love the texture and finish. Feels premium and looks professional.",
            "Amazing artwork! This artist really knows how to create stunning designs.",
            "Best sticker purchase I've made. Quality is consistently excellent here.",
            "Incredible attention to detail. You can see the care put into this design.",
            "Perfect for my gaming setup! Fits the aesthetic perfectly.",
            "Great for decorating my journal. Adds the perfect touch of personality.",
            "My daughter loves these on her school binder. Cute and colorful!",
            "Excellent for my craft projects. Easy to work with and great results.",
            "Perfect addition to my car's rear window. Shows my personality perfectly.",
            "Great for my phone case. Durable and doesn't interfere with wireless charging.",
            "Love using these for my small business packaging. Customers love them too!",
            "Perfect for my toolbox at work. Still looks great after months of use.",
            "Great for organizing my storage boxes. Both functional and decorative.",
            "Excellent for my travel luggage. Makes it easy to spot at baggage claim.",

            // 4-star reviews (very good)
            "Really nice quality sticker, arrived on time. Very satisfied overall.",
            "Good product, slightly smaller than expected but still love the design.",
            "Decent quality for the price. Would definitely consider ordering again.",
            "Nice addition to my collection. Good colors and clean printing.",
            "Pretty good quality, sticks well. Minor air bubbles but otherwise great.",
            "Good value for money. Not perfect but definitely worth the purchase.",
            "Nice design and good adhesion. Took a bit longer to ship than expected.",
            "Solid quality sticker. Colors are good, maybe slightly different from photo.",
            "Happy with this purchase. Good size and the material feels durable.",
            "Good quality overall. Easy to apply and looks nice once positioned.",
            "Nice sticker, good quality printing. Packaging could have been better.",
            "Pretty happy with this. Good design and reasonable price point.",
            "Good product, does what it's supposed to. Would recommend for the price.",
            "Nice colors and design. Application was straightforward and clean.",
            "Good quality sticker. Arrived safely packaged and in good condition.",

            // 3-star reviews (average)
            "It's okay. Not bad but not amazing either. Average quality for the price.",
            "Decent sticker. Colors are fine but not as vibrant as I hoped.",
            "Fair quality. Does the job but I've seen better for similar prices.",
            "It's alright. Nothing spectacular but serves its purpose adequately.",
            "Average product. Not disappointed but not particularly impressed either.",
            "Okay quality. Sticks well enough but the colors seem a bit faded.",
            "It's fine for what it is. Would probably look elsewhere next time.",
            "Decent enough. Got the job done but expected slightly better quality.",
            "Not bad, not great. Middle-of-the-road product at a fair price.",
            "Average sticker. Colors are okay, size is as expected. Nothing special.",

            // 2-star reviews (below average)
            "Not quite what I expected. Colors are duller than shown in the image.",
            "Quality is mediocre at best. Had some issues with the adhesive.",
            "Disappointing quality for the price. Expected much better materials.",
            "The sticker is okay but feels cheap. Probably won't order again.",
            "Not impressed. Colors faded quickly and edges started peeling.",
            "Below average quality. Had trouble getting it to stick properly.",
            "Mediocre product. Design is nice but execution could be much better.",
            "Not great quality. Corners started lifting after just a few days.",

            // 1-star reviews (poor)
            "Poor quality. Colors were completely different from what was shown.",
            "Terrible adhesive - wouldn't stick properly and kept peeling off.",
            "Very disappointed. Cheap material that started fading immediately.",
            "Worst sticker I've bought. Completely fell apart within a week.",

            // Empty reviews (star-only ratings)
            "", "", "", "", "", "", "", "", "", ""
        ];

            // Weighted star ratings: realistic e-commerce distribution
            // 50% 5-star, 25% 4-star, 15% 3-star, 7% 2-star, 3% 1-star
            int[] starPool = [5,5,5,5,5,5,5,5,5,5, 4,4,4,4,4, 3,3,3, 2,2, 1];

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
                    var starRating  = starPool[rng.Next(starPool.Length)];
                    var reviewText  = reviewTexts[rng.Next(reviewTexts.Length)];
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

    #endregion

    #region Store Review Seeding

    /// <summary>
    /// Seeds the database with featured store reviews from verified buyers.
    /// These are the testimonials displayed on the homepage carousel.
    /// Fully idempotent—skipped if any store reviews already exist.
    /// </summary>
    /// <param name="db">Database context for creating store reviews.</param>
    /// <param name="userManager">ASP.NET Core Identity UserManager for creating reviewer accounts.</param>
    /// <remarks>
    /// <para><strong>Featured Reviews:</strong></para>
    /// Seeds 3 verified buyer testimonials with 5-star ratings:
    /// <list type="bullet">
    /// <item>Lovedeep - "Great quality and super fast!"</item>
    /// <item>Evan - "I'm loving it!"</item>
    /// <item>Kimberly - "Durable and looks stunning!"</item>
    /// </list>
    ///
    /// <para><strong>Verified Buyer Status:</strong></para>
    /// All seeded reviews are marked as IsVerifiedBuyer = true and pre-approved
    /// for immediate display on the homepage.
    ///
    /// <para><strong>User Accounts:</strong></para>
    /// Creates dummy user accounts with email format: firstname.store@stickit.local
    /// These accounts are for display purposes only and cannot be used for login.
    /// </remarks>
    public static async Task SeedStoreReviewsAsync(
        ApplicationDbContext db,
        UserManager<IdentityUser> userManager)
    {
        // Idempotency: skip if any store reviews exist
        if (await db.StoreReviews.AnyAsync())
            return;

        var reviews = new[]
        {
            new
            {
                FirstName = "Lovedeep",
                Email = "lovedeep.store@stickit.local",
                Title = "Outstanding Quality!",
                Rating = 5,
                Description = "The cuts are clean, the colors pop, and shipping was quicker than I expected. I'm already planning my next order."
            },
            new
            {
                FirstName = "Evan",
                Email = "evan.store@stickit.local",
                Title = "Perfect for Business Use",
                Rating = 5,
                Description = "The purchase was simple, checkout was smooth, and the stickers look premium. Perfect for my small business packaging."
            },
            new
            {
                FirstName = "Kimberly",
                Email = "kimberly.store@stickit.local",
                Title = "Durable and Vibrant",
                Rating = 5,
                Description = "I put mine on my laptop and water bottle, still looks brand new. The finish is smooth and the print is sharp."
            }
        };

        foreach (var review in reviews)
        {
            // Create identity user
            var identityUser = new IdentityUser
            {
                UserName = review.Email,
                Email = review.Email,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(identityUser, "StoreReview@2025!");
            if (!result.Succeeded)
            {
                // User might already exist, try to find them
                identityUser = await userManager.FindByEmailAsync(review.Email);
                if (identityUser == null)
                    continue; // Skip if we can't create or find the user
            }

            // Assign Customer role
            if (!await userManager.IsInRoleAsync(identityUser, "Customer"))
            {
                await userManager.AddToRoleAsync(identityUser, "Customer");
            }

            // Create registered user entry
            var registeredUser = await db.RegisteredUsers
                .FirstOrDefaultAsync(ru => ru.Email == review.Email);

            if (registeredUser == null)
            {
                registeredUser = new RegisteredUserModel
                {
                    Email = review.Email
                };
                db.RegisteredUsers.Add(registeredUser);
                await db.SaveChangesAsync();
            }

            // Create user profile
            var profile = new UserProfileModel
            {
                PkEmail = review.Email,
                FirstName = review.FirstName,
                LastName = "",
                AvatarData = null,
                AvatarMimeType = null
            };
            db.UserProfiles.Add(profile);
            await db.SaveChangesAsync();

            // Create store review
            var storeReview = new StoreReviewModel
            {
                FkRegisteredUserId = registeredUser.PkRegisteredUserId,
                Title = review.Title,
                Rating = review.Rating,
                Description = review.Description,
                CreatedAt = DateTime.UtcNow.AddDays(-new Random().Next(7, 60)), // Random date in past 7-60 days
                Approved = true,
                IsVerifiedBuyer = true,
                IsFlagged = false,
                IsDeleted = false
            };
            db.StoreReviews.Add(storeReview);
        }

        await db.SaveChangesAsync();
    }

    #endregion
}
