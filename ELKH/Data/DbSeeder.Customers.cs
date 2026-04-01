using ELKH.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Data;

/// <summary>
/// Database seeding operations for demo customers, orders, and related business data.
/// Handles creation of customer profiles, contact details, wishlists, orders, and product reviews.
/// </summary>
/// <remarks>
/// TABLE OF CONTENTS (472 lines)
/// ================================================================================
/// 1. Main Seeding Entry Point ................................... Lines   51-95
///    - SeedCustomersAsync()               // Primary method to seed all customer data
///    - Idempotency check and orchestration of customer creation process
/// 
/// 2. Data Pool Definitions ...................................... Lines   61-141
///    - First and last name arrays for realistic customer generation
///    - Canadian cities and provinces with postal code prefixes
///    - Street names and suffixes for address generation
///    - Review content pool and star rating distributions
/// 
/// 3. Customer Account Creation ................................... Lines  142-195
///    - Identity user creation with ASP.NET Core Identity
///    - RegisteredUserModel and UserProfileModel creation
///    - Email format: firstname.lastnameN@home.com with predictable passwords
///    - Avatar image handling and profile data generation
/// 
/// 4. Contact Details Generation ................................. Lines  196-231
///    - CreateContactDetailAsync()         // Realistic Canadian addresses
///    - Canadian postal code generation (A1A 1A1 format)
///    - Phone number generation in North American format
///    - Province-specific postal code prefixes for geographical accuracy
/// 
/// 5. Wishlist Creation ....................................... Lines  233-260
///    - CreateCustomerWishlistAsync()      // 2-4 random products per customer
///    - Random product selection for realistic wish lists
///    - WishList and WishListItem model creation
/// 
/// 6. Order Generation & Processing .............................. Lines  262-340
///    - CreateCustomerOrdersAsync()        // 1-3 orders per customer
///    - Realistic order status distribution (40% Delivered, 30% Shipped, etc.)
///    - Random order item quantities and shipping method selection
///    - Order date generation within last 6 months
/// 
/// 7. Product Review Generation .................................. Lines  342-390
///    - CreateProductReviewsAsync()        // Weighted 4-5 star distribution
///    - Realistic review text from predefined content pools
///    - Star rating distribution: 50% 5-star, 25% 4-star, 15% 3-star, etc.
///    - Review content categorized by rating level
/// 
/// 8. Review Content Pool ....................................... Lines  392-471
///    - GetReviewTextPool()               // Realistic review text by rating
///    - 5-star to 1-star review templates with appropriate sentiment
///    - Empty review options for star-only ratings
/// ================================================================================
/// 
/// ARCHITECTURAL CONTEXT:
/// • Part of DbSeeder partial class system for organized database seeding
/// • Integrates with ASP.NET Core Identity for user account creation
/// • Uses Entity Framework Core for all database operations with proper SaveChanges coordination
/// • Provides realistic demo data for development and testing scenarios
/// 
/// DATA GENERATION STRATEGY:
/// • Uses deterministic randomization based on index for reproducible results
/// • Employs realistic Canadian demographic and geographic data
/// • Implements weighted distributions for orders and reviews matching e-commerce patterns
/// • Generates interconnected data (users → profiles → orders → reviews) for testing workflows
/// 
/// BUSINESS LOGIC:
/// • Customer data follows realistic e-commerce patterns and demographics
/// • Order status distribution reflects typical fulfillment pipeline
/// • Review ratings skewed toward positive (4-5 stars) as in real e-commerce platforms
/// • Contact details use proper Canadian address and postal code formats
/// 
/// PERFORMANCE CONSIDERATIONS:
/// • Batch operations with SaveChangesAsync() after entity groups
/// • Idempotent design prevents duplicate data creation on repeated runs
/// • Uses pre-allocated arrays and efficient random sampling
/// • Minimal database round trips through strategic batching
/// 
/// INTEGRATION POINTS:
/// • ASP.NET Core Identity UserManager for account creation and management
/// • ApplicationDbContext for all Entity Framework operations
/// • ProductModel entities for wishlist and order item associations
/// • File system integration for placeholder avatar image loading
/// 
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
public static partial class DbSeeder
{
    #region Customer & Order Seeding

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
    public static async Task SeedCustomersAndOrdersAsync(
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

        var products = await db.Product.AsNoTracking().ToListAsync();
        if (products.Count == 0) return;

        var rng = GetRandom();

        // ══════════════════════════════════════════════════════════════════════
        // ║ Data Pools for Realistic Customer Generation                       ║
        // ║ Predefined arrays for names, locations, and review content.        ║
        // ══════════════════════════════════════════════════════════════════════

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

        // ══════════════════════════════════════════════════════════════════════
        // ║ Review Content Pool for Product Ratings                            ║
        // ║ Range from 5-star excellent to 1-star poor with realistic text.    ║
        // ══════════════════════════════════════════════════════════════════════
        string[] reviewTexts = GetReviewTextPool();

        // Weighted star ratings: realistic e-commerce distribution
        // 50% 5-star, 25% 4-star, 15% 3-star, 7% 2-star, 3% 1-star
        int[] starPool = [5,5,5,5,5,5,5,5,5,5, 4,4,4,4,4, 3,3,3, 2,2, 1];

        // ══════════════════════════════════════════════════════════════════════
        // ║ Generate 50 Demo Customers                                         ║
        // ║ Create complete customer profiles with orders and reviews.         ║
        // ══════════════════════════════════════════════════════════════════════

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
            await CreateCustomerContactAsync(db, registeredUser, firstName, lastName, locations, streetNames, streetSuffixes, rng);

            // 5. Wishlist with 2-4 random products
            await CreateCustomerWishlistAsync(db, registeredUser, products, rng);

            // 6. Orders with items, transactions, and reviews
            await CreateCustomerOrdersAsync(db, registeredUser, products, starPool, reviewTexts, rng);

            await db.SaveChangesAsync();
        }
    }

    #endregion

    #region Customer Creation Helper Methods

    /// <summary>
    /// Creates a default contact/shipping address for a customer with realistic Canadian data.
    /// </summary>
    private static async Task CreateCustomerContactAsync(
        ApplicationDbContext db,
        RegisteredUserModel registeredUser,
        string firstName,
        string lastName,
        (string City, string Province, char Prefix)[] locations,
        string[] streetNames,
        string[] streetSuffixes,
        Random rng)
    {
        var loc = locations[rng.Next(locations.Length)];
        var streetNum = rng.Next(1, 9999);
        var streetName = streetNames[rng.Next(streetNames.Length)];
        var streetSfx = streetSuffixes[rng.Next(streetSuffixes.Length)];

        // Canadian postal code format: A1A 1A1
        // loc.Prefix supplies the FSA letter (first character, region-specific).
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
    }

    /// <summary>
    /// Creates a wishlist with 2-4 random products for a customer.
    /// </summary>
    private static async Task CreateCustomerWishlistAsync(
        ApplicationDbContext db,
        RegisteredUserModel registeredUser,
        List<ProductModel> products,
        Random rng)
    {
        var wishlist = new WishListModel { FkUserId = registeredUser.PkRegisteredUserId };
        db.WishLists.Add(wishlist);
        await db.SaveChangesAsync();

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
    }

    /// <summary>
    /// Creates 1-3 orders per customer with realistic status distribution, items, transactions, and reviews.
    /// </summary>
    private static async Task CreateCustomerOrdersAsync(
        ApplicationDbContext db,
        RegisteredUserModel registeredUser,
        List<ProductModel> products,
        int[] starPool,
        string[] reviewTexts,
        Random rng)
    {
        var contact = await db.ContactDetails.FirstAsync(c => c.FkRegisteredUserId == registeredUser.PkRegisteredUserId);
        int orderCount = rng.Next(1, 4);

        for (int o = 0; o < orderCount; o++)
        {
            var orderDate = DateTime.UtcNow.AddDays(-rng.Next(15, 400));

            // Weight order statuses: ~40 % delivered, ~30 % shipped, ~20 % processing, ~10 % pending
            int roll = rng.Next(10);
            var (orderStatus, deliveryStatus) = roll switch
            {
                < 4 => (OrderStatus.Shipped,    DeliveryStatus.Delivered),
                < 7 => (OrderStatus.Shipped,    DeliveryStatus.Shipped),
                < 9 => (OrderStatus.Shipped,    DeliveryStatus.InTransit),
                _   => (OrderStatus.Pending,    DeliveryStatus.Pending)
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
            if (orderStatus is OrderStatus.Shipped)
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

            // Review for one item from delivered/shipped orders (~66% chance per order)
            if (orderStatus is OrderStatus.Shipped && rng.Next(3) > 0)
            {
                await CreateProductReviewAsync(db, registeredUser, orderItems, starPool, reviewTexts, orderDate, rng);
            }
        }
    }

    /// <summary>
    /// Creates a product review for a random item from a customer's order.
    /// </summary>
    private static async Task CreateProductReviewAsync(
        ApplicationDbContext db,
        RegisteredUserModel registeredUser,
        List<OrderItemModel> orderItems,
        int[] starPool,
        string[] reviewTexts,
        DateTime orderDate,
        Random rng)
    {
        var ratedItem = orderItems[rng.Next(orderItems.Count)];
        var starRating = starPool[rng.Next(starPool.Length)];
        var reviewText = reviewTexts[rng.Next(reviewTexts.Length)];
        var reviewDate = orderDate.AddDays(rng.Next(2, 21));

        // Guard: one rating per order item per user
        bool alreadyRated = await db.ProductRatings.AnyAsync(r =>
            r.FkOrderItemId == ratedItem.PkOrderItemId &&
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

    /// <summary>
    /// Gets the pool of review texts for product ratings spanning all star levels.
    /// </summary>
    private static string[] GetReviewTextPool() =>
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

    #endregion
}