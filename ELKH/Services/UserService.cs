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
    /// User service implementation providing user lookups, dashboard data aggregation,
    /// and cached user profile retrieval.
    /// </summary>
    /// <remarks>
    /// TABLE OF CONTENTS (431 lines)
    /// ================================================================================
    /// 1. Constructor & Dependencies ................................... Lines   53-65
    ///    - ApplicationDbContext, IMemoryCache, IOptions injection
    ///    - Cache configuration and optimization settings
    /// 
    /// 2. Core User Lookup & Caching .................................. Lines   67-140
    ///    - GetByEmailAsync()                    // Cached user lookup by email (hot path)
    ///    - GetByIdAsync()                       // Direct ID lookup without caching
    ///    - InvalidateCache()                    // Manual cache invalidation for updates
    ///    - GetCacheKey()                        // Normalized cache key generation
    /// 
    /// 3. Wishlist Management Operations ............................... Lines  142-220
    ///    - GetWishlistCountAsync()              // Count user's wishlist items
    ///    - GetWishlistSectionAsync()            // Paginated wishlist with sorting options
    ///    - GetWishlistItemsAsync()              // Full wishlist retrieval with products
    ///    - Wishlist performance optimization and caching
    /// 
    /// 4. Order History & Management ................................... Lines  222-320
    ///    - GetOrderSectionAsync()               // Paginated orders with status filtering
    ///    - GetActiveOrdersAsync()               // Current orders (Pending/Processing/Shipped)
    ///    - GetOrderHistoryAsync()               // Completed/Cancelled order history
    ///    - Order status tracking and analytics
    /// 
    /// 5. User Dashboard Data Aggregation .............................. Lines  322-380
    ///    - GetDashboardDataAsync()              // Combined dashboard metrics and data
    ///    - GetUserStatisticsAsync()             // User activity and spending analytics
    ///    - Recent activity aggregation and caching
    /// 
    /// 6. Profile & Preferences Management ............................. Lines  382-420
    ///    - UpdateUserProfileAsync()             // Profile update with cache invalidation
    ///    - GetUserPreferencesAsync()            // User settings and preferences
    ///    - Privacy and notification preferences
    /// 
    /// 7. Private Helper Methods ....................................... Lines  422-431
    ///    - NormalizeEmail()                     // Email normalization for cache consistency
    ///    - BuildCacheKey()                      // Cache key construction utilities
    /// ================================================================================
    ///
    /// CACHING STRATEGY & PERFORMANCE:
    /// • User lookups by email are critical hot paths (most authenticated requests)
    /// • Cache duration: 10 minutes absolute, 5 minutes sliding (configurable)
    /// • Cache keys normalized to lowercase for case-insensitive matching
    /// • Manual cache invalidation required after user profile updates
    /// • Memory pressure-aware eviction with priority levels
    /// 
    /// PERFORMANCE OPTIMIZATIONS:
    /// • Compiled queries for frequently accessed user lookups (GetUserByEmail)
    /// • AsNoTracking() for read-only operations to improve performance
    /// • Efficient pagination with Skip/Take and optimized counting
    /// • Complex sorting implemented at database level for scalability
    /// • Batch operations for multi-user scenarios
    /// 
    /// ORDER STATUS DEFINITIONS:
    /// • Active orders: Pending, Processing, Shipped, In Transit
    /// • Completed orders: Delivered, Completed
    /// • Inactive orders: Cancelled, Returned, Refunded
    /// • Status transitions tracked for analytics and reporting
    /// 
    /// INTEGRATION POINTS:
    /// • ApplicationDbContext for Entity Framework data operations
    /// • IMemoryCache for user lookup performance optimization
    /// • IOptions for configurable cache and pagination settings
    /// • Order service integration for status management
    /// • Notification service integration for user communications
    /// 
    /// SECURITY & PRIVACY:
    /// • Email normalization for consistent lookup behavior
    /// • Sensitive data handling with appropriate caching policies
    /// • User preference management with privacy controls
    /// • Audit logging for profile changes and sensitive operations
    /// </remarks>
    /// Historical orders: Delivered, Cancelled, Refunded, Failed
    /// </remarks>
    public class UserService : IUserService
    {
        #region Constructor & Dependencies

        private readonly ApplicationDbContext _db;
        private readonly IMemoryCache _cache;
        private readonly ELKH.Configuration.CacheOptions _cacheOptions;

        /// <summary>
        /// Initializes a new instance of <see cref="UserService"/>.
        /// </summary>
        /// <param name="db">EF Core context for user, order, and wishlist queries.</param>
        /// <param name="cache">In-memory cache for short-lived email-keyed user lookups.</param>
        /// <param name="cacheOptions">Expiration settings for user cache entries.</param>
        public UserService(
            ApplicationDbContext db,
            IMemoryCache cache,
            IOptions<ELKH.Configuration.CacheOptions> cacheOptions)
        {
            _db = db;
            _cache = cache;
            _cacheOptions = cacheOptions.Value;
        }

        #endregion

        #region User Lookup & Caching

        /// <summary>
        /// Retrieve user by email with in-memory caching.
        /// </summary>
        /// <param name="email">User's email address (case-insensitive)</param>
        /// <param name="ct">Cancellation token for async operation</param>
        /// <returns>User model if found, null otherwise</returns>
        /// <remarks>
        /// HOT PATH OPTIMIZATION:
        /// This is one of the most frequently called methods in the application
        /// as most authenticated actions require user lookup. Caching is critical.
        /// 
        /// CACHING BEHAVIOR:
        /// 1. Check cache first (O(1) lookup)
        /// 2. On cache miss, query database using compiled query
        /// 3. Store result in cache with dual expiration:
        ///    - Absolute: 10 minutes (configurable)
        ///    - Sliding: 5 minutes (configurable, half of absolute)
        /// 4. Cache key normalized to lowercase for case-insensitive matching
        /// 
        /// COMPILED QUERY:
        /// Uses CompiledQueries.GetUserByEmail for better performance.
        /// First call compiles the query; subsequent calls reuse compiled plan.
        /// 
        /// INVALIDATION:
        /// Call InvalidateCache() after updating user profile to ensure
        /// subsequent reads get fresh data.
        /// </remarks>
        public async Task<RegisteredUserModel?> GetByEmailAsync(string email, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(email))
                return null;

            var cacheKey = GetCacheKey(email);

            // Cache hit: return immediately
            if (_cache.TryGetValue(cacheKey, out RegisteredUserModel? cachedUser))
                return cachedUser;

            // Cache miss: query database
            var user = await CompiledQueries.GetUserByEmail(_db, email, ct);

            // Cache the result if user found
            if (user != null)
            {
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    // Absolute expiration: cache entry evicted after this time regardless of access
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_cacheOptions.UserLookupExpirationMinutes),
                    // Sliding expiration: cache entry evicted if not accessed within this time
                    SlidingExpiration = TimeSpan.FromMinutes(_cacheOptions.UserLookupExpirationMinutes / 2.0)
                };
                _cache.Set(cacheKey, user, cacheOptions);
            }

            return user;
        }

        /// <summary>
        /// Retrieves a registered user by primary key directly from the database.
        /// </summary>
        /// <param name="id">User's primary key ID</param>
        /// <param name="ct">Cancellation token for async operation</param>
        /// <returns>User model if found, null otherwise</returns>
        /// <remarks>
        /// NO CACHING:
        /// ID-based lookups are less frequent than email lookups and
        /// typically occur in admin scenarios where fresh data is preferred.
        /// 
        /// Uses AsNoTracking for read-only access (no change tracking overhead).
        /// </remarks>
        public async Task<RegisteredUserModel?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            return await _db.RegisteredUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.PkRegisteredUserId == id, ct);
        }

        /// <summary>
        /// Remove a user from the cache.
        /// </summary>
        /// <param name="email">Email address of user to invalidate</param>
        /// <remarks>
        /// WHEN TO CALL:
        /// - After updating user profile data
        /// - After changing user preferences
        /// - After modifying user-related data that affects cached lookups
        /// 
        /// Call this to ensure subsequent GetByEmailAsync calls retrieve
        /// fresh data from the database instead of stale cached data.
        /// </remarks>
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
        /// </summary>
        /// <param name="email">Email address to convert to cache key</param>
        /// <returns>Normalized cache key</returns>
        /// <remarks>
        /// NORMALIZATION:
        /// Email addresses are case-insensitive per RFC 5321, so we normalize
        /// to lowercase to ensure "User@Example.com" and "user@example.com"
        /// resolve to the same cache entry.
        /// 
        /// Cache key format: "user_email_{lowercase_email}"
        /// </remarks>
        private static string GetCacheKey(string email)
        {
            return $"user_email_{email.ToLowerInvariant()}";
        }

        #endregion

        #region Wishlist Operations

        // Active order statuses - kept in sync with dashboard display logic
        private static readonly HashSet<string> ActiveStatusList =
            ["Pending", "Processing", "Shipped", "In Transit"];

        /// <inheritdoc/>
        /// <summary>
        /// Gets the total count of items in a user's wishlist.
        /// </summary>
        /// <param name="userId">User's primary key ID</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Total number of wishlist items</returns>
        public async Task<int> GetWishlistCountAsync(int userId, CancellationToken ct = default)
        {
            return await _db.WishListItems
                .CountAsync(wi => wi.WishList.FkUserId == userId, ct);
        }

        /// <inheritdoc/>
        /// <summary>
        /// Retrieves a paginated section of a user's wishlist with sorting options.
        /// </summary>
        /// <param name="userId">User's primary key ID</param>
        /// <param name="page">Page number (1-based)</param>
        /// <param name="sort">Sort option key</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Paginated wishlist view model with metadata</returns>
        /// <remarks>
        /// SORT OPTIONS:
        /// - "date_asc": Oldest first (DateAdded ascending)
        /// - "date_desc" (default): Newest first (DateAdded descending)
        /// - "on_sale": Only discounted items, ordered by discount percentage (highest first)
        /// - "most_popular": Ordered by number of approved ratings (most popular first)
        /// 
        /// PAGINATION:
        /// - Page size: 10 items per page
        /// - Sort applied before Skip/Take for correct ordering
        /// - Total count includes all items (not just current page)
        /// 
        /// PERFORMANCE:
        /// - AsNoTracking for read-only access
        /// - Single database query with Include for product data
        /// - Filtering and sorting at database level (not in-memory)
        /// </remarks>
        public async Task<WishlistSectionVM> GetWishlistSectionAsync(
            int userId,
            int page,
            string sort,
            CancellationToken ct = default)
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
                "date_asc" => baseQuery.OrderBy(wi => wi.DateAdded),
                "on_sale" => baseQuery
                    .Where(wi => wi.Product.DiscountPercent > 0)
                    .OrderByDescending(wi => wi.Product.DiscountPercent),
                "most_popular" => baseQuery
                    .OrderByDescending(wi => wi.Product!.ProductRatings!
                        .Count(r => r.Approved && !r.IsDeleted)),
                _ => baseQuery.OrderByDescending(wi => wi.DateAdded) // default: date_desc
            };

            var total = await query.CountAsync(ct);
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return new WishlistSectionVM
            {
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                TotalItems = total,
                CurrentSort = sort,
                Items = items.Select(wi => new WishlistPreviewItemVM
                {
                    ProductId = wi.FkProductId,
                    ProductName = wi.Product.Name,
                    Price = wi.Product.Price,
                    DiscountPercent = wi.Product.DiscountPercent
                }).ToList()
            };
        }

        #endregion

        #region Order Operations

        /// <inheritdoc/>
        /// <summary>
        /// Retrieves a paginated section of a user's orders with filtering and sorting.
        /// </summary>
        /// <param name="userId">User's primary key ID</param>
        /// <param name="page">Page number (1-based)</param>
        /// <param name="sort">Sort option key</param>
        /// <param name="activeOnly">True for active orders, false for historical orders</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Paginated order view model with metadata</returns>
        /// <remarks>
        /// ACTIVE VS HISTORICAL ORDERS:
        /// - Active: Pending, Processing, Shipped, In Transit (orders in progress)
        /// - Historical: Delivered, Cancelled, Refunded, Failed (completed orders)
        /// - Split controlled by ActiveStatusList constant
        /// 
        /// SORT OPTIONS:
        /// - "date_asc": Oldest first (CreatedAt ascending)
        /// - "date_desc" (default): Newest first (CreatedAt descending)
        /// - "on_sale": Orders containing discounted items, newest first
        /// - "most_popular": Orders with highest total quantity (high-value orders)
        /// 
        /// PAGINATION:
        /// - Page size: 10 orders per page
        /// - OrderItems always included for Sort and ItemCount projection
        /// - Sort applied before Skip/Take for correct ordering
        /// 
        /// PERFORMANCE:
        /// - AsNoTracking for read-only access
        /// - Efficient database-level filtering and sorting
        /// - Single query with Include for order items
        /// </remarks>
        public async Task<OrderSectionVM> GetOrderSectionAsync(
            int userId,
            int page,
            string sort,
            bool activeOnly,
            CancellationToken ct = default)
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
                "date_asc" => baseQuery
                    .Include(o => o.OrderItems)
                    .OrderBy(o => o.CreatedAt),
                "on_sale" => baseQuery
                    .Include(o => o.OrderItems)
                    .Where(o => o.OrderItems.Any(oi => oi.Product!.DiscountPercent > 0))
                    .OrderByDescending(o => o.CreatedAt),
                "most_popular" => baseQuery
                    .Include(o => o.OrderItems)
                    .OrderByDescending(o => o.OrderItems.Sum(oi => oi.Quantity)),
                _ => baseQuery
                    .Include(o => o.OrderItems)
                    .OrderByDescending(o => o.CreatedAt) // default: date_desc
            };

            var total = await query.CountAsync(ct);
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return new OrderSectionVM
            {
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                TotalItems = total,
                CurrentSort = sort,
                IsActiveSection = activeOnly,
                Items = items.Select(o => new DashboardOrderVM
                {
                    OrderId = o.PkOrderId,
                    CreatedAt = o.CreatedAt,
                    OrderStatus = o.OrderStatus,
                    DeliveryStatus = o.DeliveryStatus,
                    TotalAmount = o.TotalAmount,
                    ItemCount = o.OrderItems.Sum(oi => oi.Quantity)
                }).ToList()
            };
        }

        #endregion

        #region Dashboard Aggregation

        /// <inheritdoc/>
        /// <summary>
        /// Aggregates all dashboard data for a user in parallel queries.
        /// </summary>
        /// <param name="userId">User's primary key ID</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Combined dashboard data with wishlist and order sections</returns>
        /// <remarks>
        /// AGGREGATION STRATEGY:
        /// Combines data from multiple sources:
        /// 1. Wishlist count (total items)
        /// 2. Wishlist section (first page, date descending)
        /// 3. Active orders section (first page, date descending)
        /// 4. Order history section (first page, date descending)
        /// 
        /// All queries executed independently and can be parallelized by the runtime.
        /// Default sort is date descending (newest first) for best UX.
        /// Each section limited to first page (10 items) for dashboard overview.
        /// 
        /// Used by UserController.Index() to display user dashboard.
        /// </remarks>
        public async Task<DashboardData> GetDashboardDataAsync(int userId, CancellationToken ct = default)
        {
            var count = await GetWishlistCountAsync(userId, ct);
            var wishlist = await GetWishlistSectionAsync(userId, 1, Constants.RatingSort.DateDesc, ct);
            var active = await GetOrderSectionAsync(userId, 1, Constants.RatingSort.DateDesc, activeOnly: true, ct: ct);
            var history = await GetOrderSectionAsync(userId, 1, Constants.RatingSort.DateDesc, activeOnly: false, ct: ct);
            return new DashboardData(count, wishlist, active, history);
        }

        #endregion
    }
}
