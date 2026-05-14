using ELKH.Data;
using ELKH.Repositories;
using ELKH.Services;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Threading.RateLimiting;

namespace ELKH.Extensions
{
    /// <summary>
    /// Extension methods for IServiceCollection to organize service registrations.
    /// This keeps Program.cs clean and groups related services together.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Configure all application options from configuration sections.
        /// Binds strongly-typed option classes to their corresponding appsettings.json sections.
        /// </summary>
        public static IServiceCollection AddApplicationOptions(
            this IServiceCollection services, 
            IConfiguration configuration)
        {
            services.Configure<ELKH.Configuration.CacheOptions>(configuration.GetSection("Cache"));
            services.Configure<ELKH.Configuration.SearchOptions>(configuration.GetSection("Search"));
            services.Configure<ELKH.Configuration.EmailOptions>(configuration.GetSection("Email"));
            services.Configure<ELKH.Configuration.ModerationOptions>(configuration.GetSection("Moderation"));
            services.Configure<ELKH.Configuration.PayPalOptions>(configuration.GetSection("PayPal"));
            services.Configure<ELKH.Configuration.ReCaptchaOptions>(configuration.GetSection("ReCaptcha"));

            return services;
        }
        /// <summary>
        /// Register all application-specific services (search, rating, moderation, etc.)
        /// </summary>
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<ISearchService, SearchService>();
            services.AddScoped<IRatingService, RatingService>();
            services.AddScoped<IModerationService, ModerationService>();
            services.AddScoped<IProductService, ProductService>();
            services.AddScoped<ICartService, CartService>();
            services.AddScoped<IWishlistService, WishlistService>();
            services.AddScoped<IShippingService, ShippingService>();
            services.AddScoped<ICouponService, CouponService>(); // Coupon and promotional system
            services.AddScoped<IOrderEmailService, OrderEmailService>();
            services.AddScoped<IStoreReviewService, StoreReviewService>(); // Store review system
            services.AddScoped<IStockNotificationService, StockNotificationService>(); // Back-in-stock notifications
            services.AddScoped<StockNotificationEmailService>(); // Email notifications for restocked items
            services.AddScoped<IProductMapper, ProductMapper>(); // Manual mapping instead of AutoMapper
            services.AddScoped<ImageValidationService>(); // Secure image upload validation
            services.AddScoped<IImageOptimizationService, ImageOptimizationService>(); // Image optimization services
            services.AddScoped<IStructuredLoggingService, StructuredLoggingService>(); // Enhanced logging
            services.AddScoped<IGuestCartService, GuestCartService>(); // Guest checkout services

            return services;
        }

        /// <summary>
        /// Register all repositories following the repository pattern.
        /// </summary>
        public static IServiceCollection AddRepositories(this IServiceCollection services)
        {
            services.AddScoped<IRoleRepo, RoleRepo>();
            services.AddScoped<IOrderManagementRepo, OrderManagementRepo>();
            services.AddScoped<IRegisteredUserLogRepo, RegisteredUserLogRepo>();
            services.AddScoped<IRegisteredUserProfileRepo, RegisteredUserProfileRepo>();
            services.AddScoped<IContactDetailRepo, ContactDetailRepo>();
            services.AddScoped<IOrderHistoryManagementRepo, OrderHistoryManagementRepo>();
            services.AddScoped<IInventoryRepo, InventoryRepo>();
            services.AddScoped<IOrderRepo, OrderRepo>();
            services.AddScoped<ITransactionRepo, TransactionRepo>();

            // Concrete repositories that don't have interfaces yet
            services.AddScoped<OrderHistoryManagementRepo>();
            services.AddScoped<InventoryRepo>();
            services.AddScoped<RegisteredUserLogRepo>();
            services.AddScoped<RegisteredUserProfileRepo>();
            services.AddScoped<ContactDetailRepo>();
            services.AddScoped<TransactionRepo>();
            services.AddScoped<OrderHistoryStaffRepo>();

            return services;
        }

        /// <summary>
        /// Register email services with adapter pattern for Identity compatibility.
        /// In Development, FileEmailSender writes emails to disk. In all other environments,
        /// SmtpEmailSender wrapped in EmailSenderAdapter is used. Only the active sender is
        /// instantiated per request scope; the unused implementation is never created.
        /// </summary>
        public static IServiceCollection AddEmailServices(this IServiceCollection services)
        {
            // Construct only the sender that is needed for the current environment.
            services.AddScoped<IEmailSender>(sp =>
            {
                var env = sp.GetRequiredService<Microsoft.Extensions.Hosting.IHostEnvironment>();
                if (env.IsDevelopment())
                    return Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance<FileEmailSender>(sp);
                var smtp = Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance<SmtpEmailSender>(sp);
                return new EmailSenderAdapter(smtp);
            });

            // Forward the Identity interface to the same scoped instance so both
            // interfaces share one object per request scope.
            services.AddScoped<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender>(sp =>
                (Microsoft.AspNetCore.Identity.UI.Services.IEmailSender)sp.GetRequiredService<IEmailSender>());

            return services;
        }

        /// <summary>
        /// Register background services and hosted services.
        /// </summary>
        public static IServiceCollection AddBackgroundServices(this IServiceCollection services)
        {
            services.AddSingleton<FuzzyHelperService>();
            services.AddSingleton<FuzzyReindexService>();
            // Expose the same singleton through the interface so controllers do not depend
            // on the concrete BackgroundService type.
            services.AddSingleton<IFuzzyReindexService>(sp => sp.GetRequiredService<FuzzyReindexService>());
            services.AddHostedService(sp => sp.GetRequiredService<FuzzyReindexService>());
            
            return services;
        }

        /// <summary>
        /// Registers ASP.NET Core built-in rate-limiting policies.
        /// Call <c>app.UseRateLimiter()</c> in the middleware pipeline after this.
        /// </summary>
        public static IServiceCollection AddRateLimitingPolicies(this IServiceCollection services)
        {
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                // Strict: 5 attempts per 60 s - protects login and registration.
                options.AddFixedWindowLimiter(RateLimitPolicies.Auth, o =>
                {
                    o.PermitLimit      = 5;
                    o.Window           = TimeSpan.FromSeconds(60);
                    o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    o.QueueLimit       = 0;
                });

                // Checkout: 3 payment attempts per 60 s per IP.
                options.AddFixedWindowLimiter(RateLimitPolicies.Checkout, o =>
                {
                    o.PermitLimit      = 3;
                    o.Window           = TimeSpan.FromSeconds(60);
                    o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    o.QueueLimit       = 0;
                });

                // Search autocomplete: 30 requests per 10 s (generous for live typing).
                options.AddSlidingWindowLimiter(RateLimitPolicies.Search, o =>
                {
                    o.PermitLimit         = 30;
                    o.Window              = TimeSpan.FromSeconds(10);
                    o.SegmentsPerWindow   = 5;
                    o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    o.QueueLimit          = 0;
                });

                // Admin operations: 10 requests per 60 s - protects resource-intensive admin actions.
                // Includes: ReindexFTS, ClearCache, bulk operations
                options.AddFixedWindowLimiter(RateLimitPolicies.Admin, o =>
                {
                    o.PermitLimit      = 10;
                    o.Window           = TimeSpan.FromSeconds(60);
                    o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    o.QueueLimit       = 0;
                });

                // Cart operations: 20 requests per 60 s - prevents inventory enumeration attacks.
                // Includes: AddToCart, Update, Remove
                options.AddFixedWindowLimiter(RateLimitPolicies.Cart, o =>
                {
                    o.PermitLimit      = 20;
                    o.Window           = TimeSpan.FromSeconds(60);
                    o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    o.QueueLimit       = 0;
                });
            });

            return services;
        }
        public static IServiceCollection AddOutputCachingPolicies(this IServiceCollection services)
        {
            services.AddOutputCache(options =>
            {
                // Default policy for most pages
                options.AddBasePolicy(builder => builder
                    .Expire(TimeSpan.FromMinutes(1))
                    .SetVaryByQuery("*"));

                // Policy for product listings - cache longer, vary by all query params
                // so search/filter/page combinations each get their own cache entry.
                // ProductListOutputCachePolicy also varies by auth state so that
                // auth-dependent markup (e.g. wishlist buttons in _ProductCard) is never
                // served to the wrong user type.
                options.AddPolicy("ProductList", new ProductListOutputCachePolicy());

                // Policy for product details - cache with user variation
                options.AddPolicy("ProductDetails", builder => builder
                    .Expire(TimeSpan.FromMinutes(2))
                    .SetVaryByQuery("id")
                    .Tag("products"));

                // "OrderDetails" policy removed: order detail pages contain personal data
                // and require a per-request ownership check, making output caching unsafe.
            });

            return services;
        }
    }
}

/// <summary>
/// Output-cache policy for product listings.<br/>
/// Varies by every query-string parameter (search, category, sort, offsetâ€¦) AND by
/// authentication state, so that auth-dependent markup â€” such as the wishlist button
/// in <c>_ProductCard.cshtml</c> â€” is never served to the wrong class of user.
/// </summary>
internal sealed class ProductListOutputCachePolicy : IOutputCachePolicy
{
    public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken cancellationToken)
    {
        context.EnableOutputCaching             = true;
        context.AllowCacheLookup                = true;
        context.AllowCacheStorage               = true;
        context.AllowLocking                    = true;
        context.ResponseExpirationTimeSpan      = TimeSpan.FromMinutes(5);

        // Vary by all query-string keys (search, categoryId, sort, offsetâ€¦)
        context.CacheVaryByRules.QueryKeys = "*";

        // Vary by auth state: authenticated and anonymous users get separate cache
        // entries, preventing the wishlist button from rendering in the wrong state.
        var authKey = context.HttpContext.User.Identity?.IsAuthenticated == true ? "1" : "0";
        context.CacheVaryByRules.VaryByValues.TryAdd("auth", authKey);

        context.Tags.Add("products");

        return ValueTask.CompletedTask;
    }

    public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellationToken)
        => ValueTask.CompletedTask;

    public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken cancellationToken)
        => ValueTask.CompletedTask;
}

namespace ELKH.Extensions
{
    // ----- rate-limiting policy names (shared between registration and attributes) -----
    public static class RateLimitPolicies
    {
        public const string Auth     = "auth";      // login / register
        public const string Checkout = "checkout";  // payment endpoints
        public const string Search   = "search";    // autocomplete
        public const string Admin    = "admin";     // resource-intensive admin operations
        public const string Cart     = "cart";      // cart operations (prevents inventory enumeration)
    }
}
