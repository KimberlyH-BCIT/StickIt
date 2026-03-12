using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ELKH.Data;
using ELKH.Models;
using ELKH.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ELKH.Services
{
    /// <summary>
    /// Implementation of user service with in-memory caching.
    /// User lookups by email are cached for 10 minutes to reduce database load.
    /// </summary>
    public class UserService : IUserService
    {
        private readonly ApplicationDbContext _db;
        private readonly IMemoryCache _cache;
        private readonly ELKH.Configuration.CacheOptions _cacheOptions;

        /// <summary>
        /// Initializes a new instance of <see cref="UserService"/>.
        /// </summary>
        /// <param name="db">EF Core context for user, order, and wishlist queries.</param>
        /// <param name="cache">In-memory cache for short-lived email-keyed user lookups.</param>
        /// <param name="cacheOptions">Expiration settings for user cache entries.</param>
        public UserService(ApplicationDbContext db, IMemoryCache cache, IOptions<ELKH.Configuration.CacheOptions> cacheOptions)
        {
            _db = db;
            _cache = cache;
            _cacheOptions = cacheOptions.Value;
        }

        /// <summary>
        /// Retrieve user by email with caching. This is a hot path in the application
        /// as most authenticated actions require user lookup.
        /// Uses compiled query for better performance.
        /// </summary>
        public async Task<RegisteredUserModel?> GetByEmailAsync(string email, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(email))
                return null;

            var cacheKey = GetCacheKey(email);

            if (_cache.TryGetValue(cacheKey, out RegisteredUserModel? cachedUser))
                return cachedUser;

            var user = await CompiledQueries.GetUserByEmail(_db, email, ct);

            if (user != null)
            {
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_cacheOptions.UserLookupExpirationMinutes),
                    SlidingExpiration               = TimeSpan.FromMinutes(_cacheOptions.UserLookupExpirationMinutes / 2.0)
                };
                _cache.Set(cacheKey, user, cacheOptions);
            }

            return user;
        }

        /// <summary>
        /// Retrieves a registered user by primary key directly from the database.
        /// Uses <c>AsNoTracking</c> for read-only access; not cached.
        /// </summary>
        public async Task<RegisteredUserModel?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            return await _db.RegisteredUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.PkRegisteredUserId == id, ct);
        }

        /// <summary>
        /// Remove a user from the cache. Call this after updating user data
        /// to ensure subsequent reads get fresh data.
        /// </summary>
        public void InvalidateCache(string email)
        {
            if (!string.IsNullOrEmpty(email))
            {
                var cacheKey = GetCacheKey(email);
                _cache.Remove(cacheKey);
            }
        }

        /// <summary>
        /// Builds a deterministic cache key from an email address.
        /// Lowercasing normalizes the key so "User@Example.com" and "user@example.com" resolve identically.
        /// </summary>
        private static string GetCacheKey(string email)
        {
            return $"user_email_{email.ToLowerInvariant()}";
        }

        // Orders whose status is considered "active" — kept in sync with the dashboard display logic.
        private static readonly HashSet<string> ActiveStatusList =
            ["Pending", "Processing", "Shipped", "In Transit"];

        /// <inheritdoc/>
        public async Task<int> GetWishlistCountAsync(int userId, CancellationToken ct = default)
        {
            return await _db.WishListItems
                .CountAsync(wi => wi.WishList.FkUserId == userId, ct);
        }

        /// <inheritdoc/>
        public async Task<WishlistSectionVM> GetWishlistSectionAsync(int userId, int page, string sort, CancellationToken ct = default)
        {
            const int pageSize = 10;

            var baseQuery = _db.WishListItems
                .AsNoTracking()
                .Where(wi => wi.WishList.FkUserId == userId)
                .Include(wi => wi.Product);

            // Apply sort before pagination so SKIP/TAKE operate on the correctly ordered set.
            // "on_sale" additionally filters to only discounted items before sorting by discount depth.
            // "most_popular" orders by the count of approved, non-deleted ratings for each product.
            IQueryable<WishListItemModel> query = sort switch
            {
                "date_asc"     => baseQuery.OrderBy(wi => wi.DateAdded),
                "on_sale"      => baseQuery
                                    .Where(wi => wi.Product.DiscountPercent > 0)
                                    .OrderByDescending(wi => wi.Product.DiscountPercent),
                "most_popular" => baseQuery
                                    .OrderByDescending(wi => wi.Product!.ProductRatings!
                                        .Count(r => r.Approved && !r.IsDeleted)),
                _              => baseQuery.OrderByDescending(wi => wi.DateAdded)
            };

            var total = await query.CountAsync(ct);
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return new WishlistSectionVM
            {
                CurrentPage = page,
                TotalPages  = (int)Math.Ceiling(total / (double)pageSize),
                TotalItems  = total,
                CurrentSort = sort,
                Items = items.Select(wi => new WishlistPreviewItemVM
                {
                    ProductId       = wi.FkProductId,
                    ProductName     = wi.Product.Name,
                    Price           = wi.Product.Price,
                    DiscountPercent = wi.Product.DiscountPercent
                }).ToList()
            };
        }

        /// <inheritdoc/>
        public async Task<OrderSectionVM> GetOrderSectionAsync(int userId, int page, string sort, bool activeOnly, CancellationToken ct = default)
        {
            const int pageSize = 10;

            var statusQuery = _db.Orders
                .AsNoTracking()
                .Where(o => o.FkRegisteredUserId == userId);

            // Split orders into active (in-progress) and historical based on their status string,
            // using ActiveStatusList as the canonical set so display logic and query logic stay in sync.
            var baseQuery = activeOnly
                ? statusQuery.Where(o => ActiveStatusList.Contains(o.OrderStatus))
                : statusQuery.Where(o => !ActiveStatusList.Contains(o.OrderStatus));

            // Include OrderItems in every branch so the Sort and ItemCount projection work correctly.
            // "on_sale" additionally narrows to orders containing at least one discounted item.
            // "most_popular" sorts by total quantity ordered (a proxy for high-value orders).
            IQueryable<OrderModel> query = sort switch
            {
                "date_asc"     => baseQuery.Include(o => o.OrderItems).OrderBy(o => o.CreatedAt),
                "on_sale"      => baseQuery.Include(o => o.OrderItems).Where(o => o.OrderItems.Any(oi => oi.Product!.DiscountPercent > 0)).OrderByDescending(o => o.CreatedAt),
                "most_popular" => baseQuery.Include(o => o.OrderItems).OrderByDescending(o => o.OrderItems.Sum(oi => oi.Quantity)),
                _              => baseQuery.Include(o => o.OrderItems).OrderByDescending(o => o.CreatedAt)
            };

            var total = await query.CountAsync(ct);
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return new OrderSectionVM
            {
                CurrentPage     = page,
                TotalPages      = (int)Math.Ceiling(total / (double)pageSize),
                TotalItems      = total,
                CurrentSort     = sort,
                IsActiveSection = activeOnly,
                Items = items.Select(o => new DashboardOrderVM
                {
                    OrderId        = o.PkOrderId,
                    CreatedAt      = o.CreatedAt,
                    OrderStatus    = o.OrderStatus,
                    DeliveryStatus = o.DeliveryStatus,
                    TotalAmount    = o.TotalAmount,
                    ItemCount      = o.OrderItems.Sum(oi => oi.Quantity)
                }).ToList()
            };
        }

        /// <inheritdoc/>
        public async Task<DashboardData> GetDashboardDataAsync(int userId, CancellationToken ct = default)
        {
            var count    = await GetWishlistCountAsync(userId, ct);
            var wishlist = await GetWishlistSectionAsync(userId, 1, Constants.RatingSort.DateDesc, ct);
            var active   = await GetOrderSectionAsync(userId, 1, Constants.RatingSort.DateDesc, activeOnly: true,  ct: ct);
            var history  = await GetOrderSectionAsync(userId, 1, Constants.RatingSort.DateDesc, activeOnly: false, ct: ct);
            return new DashboardData(count, wishlist, active, history);
        }
    }
}
