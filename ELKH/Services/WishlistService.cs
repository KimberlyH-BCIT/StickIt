namespace ELKH.Services;
    /// <summary>
    /// Handles all wishlist mutations and queries.
    /// WishlistController delegates entirely to this service - no EF access in the controller.
    /// </summary>
    /// <param name="db">EF Core context for wishlist and item mutations.</param>
    /// <param name="userService">User lookup service (cached) for resolving the acting user.</param>
    public class WishlistService(ApplicationDbContext db, IUserService userService) : IWishlistService
    {
        /// <summary>
        /// Add a product to the user's wishlist.
        /// Creates the wishlist if the user doesn't have one yet.
        /// Returns AlreadyExists = true if the product is already present.
        /// </summary>
        public async Task<WishlistResult> AddAsync(string userEmail, int productId)
        {
            var user = await userService.GetByEmailAsync(userEmail);

            // Lazily provision a RegisteredUserModel for accounts that were created
            // outside the standard registration flow (e.g. the seeded admin account).
            if (user is null)
            {
                user = new RegisteredUserModel { Email = userEmail };
                db.RegisteredUsers.Add(user);
                await db.SaveChangesAsync();
            }

            var product = await db.Products.FindAsync(productId);
            if (product is null)
                return new WishlistResult { Success = false, Message = "Product not found" };

            var wishlist = await db.WishLists
                .Include(w => w.WishListItems)
                .FirstOrDefaultAsync(w => w.FkUserId == user.PkRegisteredUserId);

            if (wishlist is null)
            {
                wishlist = new WishListModel { FkUserId = user.PkRegisteredUserId };
                db.WishLists.Add(wishlist);
                await db.SaveChangesAsync();
                wishlist.WishListItems = new List<WishListItemModel>();
            }

            if (wishlist.WishListItems.Any(wi => wi.FkProductId == productId))
            {
                // Item already exists, return count without additional DB call
                return new WishlistResult 
                { 
                    Success = false, 
                    AlreadyExists = true, 
                    Message = "Already in wishlist", 
                    Count = wishlist.WishListItems.Count 
                };
            }

            db.WishListItems.Add(new WishListItemModel
            {
                FkWishListId = wishlist.PkWishListId,
                FkProductId  = productId
            });
            await db.SaveChangesAsync();

            // Calculate count based on in-memory collection + new item (avoid DB call)
            var count = wishlist.WishListItems.Count + 1;
            return new WishlistResult { Success = true, Message = "Product added", Count = count };
        }

        /// <summary>
        /// Remove a product from the user's wishlist.
        /// </summary>
        public async Task<WishlistResult> RemoveAsync(string userEmail, int productId)
        {
            var user = await userService.GetByEmailAsync(userEmail);
            if (user is null)
                return new WishlistResult { Success = false, Message = "User not found" };

            var wishlist = await db.WishLists
                .Include(w => w.WishListItems)
                .FirstOrDefaultAsync(w => w.FkUserId == user.PkRegisteredUserId);

            if (wishlist is null)
                return new WishlistResult { Success = false, Message = "Wishlist not found" };

            var item = wishlist.WishListItems.FirstOrDefault(wi => wi.FkProductId == productId);
            if (item is null)
                return new WishlistResult { Success = false, Message = "Item not found" };

            db.WishListItems.Remove(item);
            await db.SaveChangesAsync();

            var count = await db.WishListItems.CountAsync(wi => wi.FkWishListId == wishlist.PkWishListId);
            return new WishlistResult { Success = true, Message = "Removed", Count = count };
        }

        /// <summary>
        /// Return all wishlist items for the user, sorted by DateAdded.
        /// </summary>
        public async Task<IEnumerable<WishListItemModel>> GetItemsAsync(string userEmail, string sort)
        {
            var user = await userService.GetByEmailAsync(userEmail);
            if (user is null)
                return Enumerable.Empty<WishListItemModel>();

            var wishlist = await db.WishLists
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.FkUserId == user.PkRegisteredUserId);

            if (wishlist is null)
                return Enumerable.Empty<WishListItemModel>();

            IQueryable<WishListItemModel> query = db.WishListItems
                .AsNoTracking()
                .Include(wi => wi.Product)
                .Where(wi => wi.FkWishListId == wishlist.PkWishListId);

            query = sort switch
            {
                "date_asc" => query.OrderBy(i => i.DateAdded),
                _          => query.OrderByDescending(i => i.DateAdded)
            };

            return await query.ToListAsync();
        }
    }
