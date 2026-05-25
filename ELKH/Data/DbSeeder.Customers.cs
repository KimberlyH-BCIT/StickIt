using ELKH.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Data;

// TABLE OF CONTENTS
// - Customer and order seeding
// - Contact creation
// - Wishlist creation
// - Order creation
// - Review creation
// - Review text pool

public static partial class DbSeeder
{
    public static async Task SeedCustomersAndOrdersAsync(
        ApplicationDbContext db,
        UserManager<IdentityUser> userManager,
        string wwwRootPath)
    {
        if (await db.RegisteredUsers.AnyAsync(u => u.Email.EndsWith("@home.com")))
            return;

        var avatarPath = Path.Combine(wwwRootPath, "images", "placeholder.png");
        byte[]? avatarBytes = File.Exists(avatarPath)
            ? await File.ReadAllBytesAsync(avatarPath)
            : null;

        var products = await db.Products.AsNoTracking().ToListAsync();
        if (products.Count == 0) return;

        var rng = GetRandom();

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
            "Parker","EvANS","Edwards","Collins","Stewart","Sanchez","Morris","Rogers","Reed",
            "Cook","Morgan","Bell"
        ];

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

        string[] streetSuffixes = ["St", "Ave", "Rd", "Blvd", "Cres", "Dr", "Way", "Lane", "Pl", "Ct"];
        string[] streetNames =
        [
            "Maple","Oak","Pine","Cedar","Elm","Birch","Walnut","Willow","Spruce","Ash",
            "Poplar","Cherry","Larch","Fir","Sycamore","Hazel","Beech","Alder","Rowan","Hawthorn"
        ];

        var reviewTextsByRating = GetReviewTextPool();

        int[] starPool = [5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 4, 4, 4, 4, 4, 3, 3, 3, 2, 2, 1];

        for (int i = 0; i < 50; i++)
        {
            var firstName = firstNames[i];
            var lastName = lastNames[i];
            var email = $"{firstName.ToLowerInvariant()}.{lastName.ToLowerInvariant()}{i + 1}@home.com";
            var password = $"Demo@2025!{(i + 1):D2}";

            var identityUser = new IdentityUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(identityUser, password);
            if (!result.Succeeded) continue;

            await userManager.AddToRoleAsync(identityUser, "Customer");

            var registeredUser = new RegisteredUserModel { Email = email };
            db.RegisteredUsers.Add(registeredUser);
            await db.SaveChangesAsync();

            db.UserProfiles.Add(new UserProfileModel
            {
                PkEmail = email,
                FirstName = firstName,
                LastName = lastName,
                AvatarData = avatarBytes,
                AvatarMimeType = avatarBytes is not null ? "image/png" : null
            });

            await CreateCustomerContactAsync(db, registeredUser, identityUser.Id, firstName, lastName, locations, streetNames, streetSuffixes, rng);

            await CreateCustomerWishlistAsync(db, registeredUser, products, rng);

            await CreateCustomerOrdersAsync(db, userManager, registeredUser, products, starPool, reviewTextsByRating, rng);

            await db.SaveChangesAsync();
        }
    }

    private static async Task CreateCustomerContactAsync(
        ApplicationDbContext db,
        RegisteredUserModel registeredUser,
        string userId,
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

        var postalCode = $"{loc.Prefix}{rng.Next(1, 9)}{(char)('A' + rng.Next(26))} {rng.Next(1, 9)}{(char)('A' + rng.Next(26))}{rng.Next(1, 9)}";

        var contact = new ContactDetailModel
        {
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = $"({rng.Next(200, 999)}) {rng.Next(100, 999)}-{rng.Next(1000, 9999)}",
            Street = $"{streetNum} {streetName} {streetSfx}",
            City = loc.City,
            Province = loc.Province,
            PostCode = postalCode,
            Country = "Canada",
            IsDefault = true,
            FkRegisteredUserId = registeredUser.PkRegisteredUserId
        };
        db.ContactDetails.Add(contact);
        await db.SaveChangesAsync();
    }

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
                FkProductId = wp.PkProductId,
                DateAdded = DateTime.UtcNow.AddDays(-rng.Next(1, 180))
            });
        }
    }

    private static async Task CreateCustomerOrdersAsync(
        ApplicationDbContext db,
        UserManager<IdentityUser> userManager,
        RegisteredUserModel registeredUser,
        List<ProductModel> products,
        int[] starPool,
        Dictionary<int, string[]> reviewTextsByRating,
        Random rng)
    {
        var contact = await db.ContactDetails.FirstAsync(c => c.FkRegisteredUserId == registeredUser.PkRegisteredUserId);
        int orderCount = rng.Next(1, 4);

        for (int o = 0; o < orderCount; o++)
        {
            var orderDate = DateTime.UtcNow.AddDays(-rng.Next(1, 400));

            int roll = rng.Next(10);
            var (orderStatus, deliveryStatus) = roll switch
            {
                < 4 => (OrderStatus.Shipped, DeliveryStatus.Delivered),
                < 7 => (OrderStatus.Shipped, DeliveryStatus.Shipped),
                < 9 => (OrderStatus.Shipped, DeliveryStatus.InTransit),
                _ => (OrderStatus.Pending, DeliveryStatus.Pending)
            };

            var orderProducts = products.OrderBy(_ => rng.Next()).Take(rng.Next(1, 4)).ToList();

            var order = new OrderModel
            {
                OrderStatus = orderStatus,
                TotalAmount = 0,
                CreatedAt = orderDate,
                DeliveryStatus = deliveryStatus,
                FkRegisteredUserId = registeredUser.PkRegisteredUserId,
                FkContactId = contact.PkContactId
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
                    FkOrderId = order.PkOrderId,
                    FkProductId = prod.PkProductId,
                    Quantity = qty
                };
                db.OrderItems.Add(item);
                orderItems.Add(item);
            }
            await db.SaveChangesAsync();

            order.TotalAmount = Math.Round(orderTotal, 2);

            if (orderStatus is OrderStatus.Shipped)
            {
                db.Transactions.Add(new TransactionModel
                {
                    TransactionStatus = "Completed",
                    Amount = Math.Round(orderTotal + 5.99m, 2),
                    TransactionDate = orderDate.AddMinutes(rng.Next(5, 90)),
                    DeliveryFee = 5.99m,
                    FkOrderId = order.PkOrderId,
                    FkContactId = contact.PkContactId
                });
            }
            await db.SaveChangesAsync();

            if (orderStatus is OrderStatus.Shipped && rng.Next(3) > 0)
            {
                await CreateProductReviewAsync(db, userManager, registeredUser, orderItems, starPool, reviewTextsByRating, orderDate, rng);
            }
        }
    }

    private static async Task CreateProductReviewAsync(
        ApplicationDbContext db,
        UserManager<IdentityUser> userManager,
        RegisteredUserModel registeredUser,
        List<OrderItemModel> orderItems,
        int[] starPool,
        Dictionary<int, string[]> reviewTextsByRating,
        DateTime orderDate,
        Random rng)
    {
        var ratedItem = orderItems[rng.Next(orderItems.Count)];
        var starRating = starPool[rng.Next(starPool.Length)];
        var textsForRating = reviewTextsByRating[starRating];
        var reviewText = textsForRating[rng.Next(textsForRating.Length)];
        var reviewDate = orderDate.AddDays(rng.Next(2, 21));

        bool alreadyRated = await db.ProductRatings.AnyAsync(r =>
            r.FkOrderItemId == ratedItem.PkOrderItemId &&
            r.FkRegisteredUserId == registeredUser.PkRegisteredUserId);

        if (!alreadyRated)
        {
            var identityUser = await userManager.FindByEmailAsync(registeredUser.Email);
            if (identityUser == null) return;

            db.ProductRatings.Add(new ProductRatingModel
            {
                FkProductId = ratedItem.FkProductId,
                FkRegisteredUserId = registeredUser.PkRegisteredUserId,
                FkOrderItemId = ratedItem.PkOrderItemId,
                Rating = starRating,
                Description = reviewText,
                RatedTime = reviewDate,
                Approved = true,
                IsFlagged = false,
                UserId = identityUser.Id
            });
            await db.SaveChangesAsync();
        }
    }

    private static Dictionary<int, string[]> GetReviewTextPool() => new()
    {
        [5] =
        [
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
            "Waterproof as advertised - survived a full wash cycle without damage.",
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
            "", "", ""
        ],
        [4] =
        [
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
            "", ""
        ],
        [3] =
        [
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
            "", ""
        ],
        [2] =
        [
            "Not quite what I expected. Colors are duller than shown in the image.",
            "Quality is mediocre at best. Had some issues with the adhesive.",
            "Disappointing quality for the price. Expected much better materials.",
            "The sticker is okay but feels cheap. Probably won't order again.",
            "Not impressed. Colors faded quickly and edges started peeling.",
            "Below average quality. Had trouble getting it to stick properly.",
            "Mediocre product. Design is nice but execution could be much better.",
            "Not great quality. Corners started lifting after just a few days.",
            ""
        ],
        [1] =
        [
            "Poor quality. Colors were completely different from what was shown.",
            "Terrible adhesive - wouldn't stick properly and kept peeling off.",
            "Very disappointed. Cheap material that started fading immediately.",
            "Worst sticker I've bought. Completely fell apart within a week.",
            ""
        ]
    };

}
