namespace ELKH.Services;

public class UserService(
    ApplicationDbContext db,
    IMemoryCache cache,
    IOptions<ELKH.Configuration.CacheOptions> cacheOptions) : IUserService
{
    private readonly ELKH.Configuration.CacheOptions _cacheOptions = cacheOptions.Value;

    // Active order statuses
    private static readonly HashSet<OrderStatus> ActiveStatusList =
        [OrderStatus.Pending, OrderStatus.Paid, OrderStatus.Shipped];

    // ============================
    // USER LOOKUP
    // ============================
    public async Task<RegisteredUserModel?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(email)) return null;

        var cacheKey = GetCacheKey(email);

        if (cache.TryGetValue(cacheKey, out RegisteredUserModel? cachedUser))
            return cachedUser;

        var user = await CompiledQueries.GetUserByEmail(db, email, ct);

        if (user != null)
        {
            var cacheEntryOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_cacheOptions.UserLookupExpirationMinutes),
                SlidingExpiration = TimeSpan.FromMinutes(_cacheOptions.UserLookupExpirationMinutes / 2.0)
            };

            cache.Set(cacheKey, user, cacheEntryOptions);
        }

        return user;
    }

    public async Task<RegisteredUserModel?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await db.RegisteredUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.PkRegisteredUserId == id, ct);
    }

    public void InvalidateCache(string email)
    {
        if (!string.IsNullOrEmpty(email))
        {
            var cacheKey = GetCacheKey(email);
            cache.Remove(cacheKey);
        }
    }

    private static string GetCacheKey(string email)
    {
        return $"user_email_{email.ToLowerInvariant()}";
    }

    // ============================
    // WISHLIST
    // ============================
    public async Task<int> GetWishlistCountAsync(int userId, CancellationToken ct = default)
    {
        return await db.WishListItems
            .CountAsync(wi => wi.WishList.FkUserId == userId, ct);
    }

    public async Task<WishlistSectionVM> GetWishlistSectionAsync(
        int userId,
        int page,
        string sort,
        CancellationToken ct = default)
    {
        const int pageSize = 10;

        var baseQuery = db.WishListItems
            .AsNoTracking()
            .Where(wi => wi.WishList.FkUserId == userId)
            .Include(wi => wi.Product);

        IQueryable<WishListItemModel> query = sort switch
        {
            "date_asc" => baseQuery.OrderBy(wi => wi.DateAdded),
            "on_sale" => baseQuery
                .Where(wi => wi.Product.DiscountPercent > 0)
                .OrderByDescending(wi => wi.Product.DiscountPercent),
            "most_popular" => baseQuery
                .OrderByDescending(wi => wi.Product!.ProductRatings!
                    .Count(r => r.Approved && !r.IsDeleted)),
            _ => baseQuery.OrderByDescending(wi => wi.DateAdded)
        };

        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

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

    // ============================
    // ORDERS
    // ============================
    public async Task<OrderSectionVM> GetOrderSectionAsync(
        int userId,
        int page,
        string sort,
        bool activeOnly,
        CancellationToken ct = default)
    {
        const int pageSize = 10;

        var statusQuery = db.Orders
            .AsNoTracking()
            .Where(o => o.FkRegisteredUserId == userId);

        var baseQuery = activeOnly
            ? statusQuery.Where(o => ActiveStatusList.Contains(o.OrderStatus))
            : statusQuery.Where(o => !ActiveStatusList.Contains(o.OrderStatus));

        IQueryable<OrderModel> query = sort switch
        {
            "date_asc" => baseQuery.Include(o => o.OrderItems).OrderBy(o => o.CreatedAt),
            "on_sale" => baseQuery
                .Include(o => o.OrderItems)
                .Where(o => o.OrderItems.Any(oi => oi.Product!.DiscountPercent > 0))
                .OrderByDescending(o => o.CreatedAt),
            "most_popular" => baseQuery
                .Include(o => o.OrderItems)
                .OrderByDescending(o => o.OrderItems.Sum(oi => oi.Quantity)),
            _ => baseQuery
                .Include(o => o.OrderItems)
                .OrderByDescending(o => o.CreatedAt)
        };

        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

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
                OrderStatus = o.OrderStatus.ToString(),
                DeliveryStatus = o.DeliveryStatus.ToString(),
                TotalAmount = o.TotalAmount,
                ItemCount = o.OrderItems.Sum(oi => oi.Quantity)
            }).ToList()
        };
    }

    // ============================
    // DASHBOARD
    // ============================
    public async Task<DashboardData> GetDashboardDataAsync(int userId, CancellationToken ct = default)
    {
        var count = await GetWishlistCountAsync(userId, ct);
        var wishlist = await GetWishlistSectionAsync(userId, 1, Constants.RatingSort.DateDesc, ct);
        var active = await GetOrderSectionAsync(userId, 1, Constants.RatingSort.DateDesc, true, ct);
        var history = await GetOrderSectionAsync(userId, 1, Constants.RatingSort.DateDesc, false, ct);

        return new DashboardData(count, wishlist, active, history);
    }
}
