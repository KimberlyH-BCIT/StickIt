using ELKH.Data;
using ELKH.Models;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Services
{
    /// <summary>
    /// Handles all wishlist mutations and queries.
    /// WishlistController delegates entirely to this service — no EF access in the controller.
    /// </summary>
    public class WishlistService : IWishlistService
    {
        private readonly ApplicationDbContext _db;
        private readonly IUserService _userService;

        /// <summary>
        /// Initializes a new instance of <see cref="WishlistService"/>.
        /// </summary>
        /// <param name="db">EF Core context for wishlist and item mutations.</param>
        /// <param name="userService">User lookup service (cached) for resolving the acting user.</param>
        public WishlistService(ApplicationDbContext db, IUserService userService)
        {
            _db = db;
            _userService = userService;
        }

        /// <summary>
        /// Add a product to the user's wishlist.
        /// Creates the wishlist if the user doesn't have one yet.
        /// Returns AlreadyExists = true if the product is already present.
        /// </summary>
        public async Task<WishlistResult> AddAsync(string userEmail, int productId)
        {
            var user = await _userService.GetByEmailAsync(userEmail);

            // Lazily provision a RegisteredUserModel for accounts that were created
            // outside the standard registration flow (e.g. the seeded admin account).
            if (user is null)
            {
                user = new RegisteredUserModel { Email = userEmail };
                _db.RegisteredUsers.Add(user);
                await _db.SaveChangesAsync();
            }

            var product = await _db.Products.FindAsync(productId);
            if (product is null)
                return new WishlistResult { Success = false, Message = "Product not found" };

            var wishlist = await _db.WishLists
                .Include(w => w.WishListItems)
                .FirstOrDefaultAsync(w => w.FkUserId == user.PkRegisteredUserId);

            if (wishlist is null)
            {
                wishlist = new WishListModel { FkUserId = user.PkRegisteredUserId };
                _db.WishLists.Add(wishlist);
                await _db.SaveChangesAsync();
                wishlist.WishListItems = new List<WishListItemModel>();
            }

            if (wishlist.WishListItems.Any(wi => wi.FkProductId == productId))
            {
                var existingCount = await _db.WishListItems.CountAsync(wi => wi.FkWishListId == wishlist.PkWishListId);
                return new WishlistResult { Success = false, AlreadyExists = true, Message = "Already in wishlist", Count = existingCount };
            }

            _db.WishListItems.Add(new WishListItemModel
            {
                FkWishListId = wishlist.PkWishListId,
                FkProductId  = productId
            });
            await _db.SaveChangesAsync();

            var count = await _db.WishListItems.CountAsync(wi => wi.FkWishListId == wishlist.PkWishListId);
            return new WishlistResult { Success = true, Message = "Product added", Count = count };
        }

        /// <summary>
        /// Remove a product from the user's wishlist.
        /// </summary>
        public async Task<WishlistResult> RemoveAsync(string userEmail, int productId)
        {
            var user = await _userService.GetByEmailAsync(userEmail);
            if (user is null)
                return new WishlistResult { Success = false, Message = "User not found" };

            var wishlist = await _db.WishLists
                .Include(w => w.WishListItems)
                .FirstOrDefaultAsync(w => w.FkUserId == user.PkRegisteredUserId);

            if (wishlist is null)
                return new WishlistResult { Success = false, Message = "Wishlist not found" };

            var item = wishlist.WishListItems.FirstOrDefault(wi => wi.FkProductId == productId);
            if (item is null)
                return new WishlistResult { Success = false, Message = "Item not found" };

            _db.WishListItems.Remove(item);
            await _db.SaveChangesAsync();

            var count = await _db.WishListItems.CountAsync(wi => wi.FkWishListId == wishlist.PkWishListId);
            return new WishlistResult { Success = true, Message = "Removed", Count = count };
        }

        /// <summary>
        /// Return all wishlist items for the user, sorted by DateAdded.
        /// </summary>
        public async Task<IEnumerable<WishListItemModel>> GetItemsAsync(string userEmail, string sort)
        {
            var user = await _userService.GetByEmailAsync(userEmail);
            if (user is null)
                return Enumerable.Empty<WishListItemModel>();

            var wishlist = await _db.WishLists
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.FkUserId == user.PkRegisteredUserId);

            if (wishlist is null)
                return Enumerable.Empty<WishListItemModel>();

            IQueryable<WishListItemModel> query = _db.WishListItems
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
}
