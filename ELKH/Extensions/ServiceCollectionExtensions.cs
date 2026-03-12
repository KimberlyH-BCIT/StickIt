using ELKH.Data;
using ELKH.Repositories;
using ELKH.Services;
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
            services.AddScoped<IOrderEmailService, OrderEmailService>();

            return services;
        }

        /// <summary>
        /// Register all repositories following the repository pattern.
        /// </summary>
        public static IServiceCollection AddRepositories(this IServiceCollection services)
        {
            services.AddScoped<IRoleRepository, RoleRepository>();
            services.AddScoped<IOrderManagementRepo, OrderManagementRepo>();
            services.AddScoped<IRegisteredUserLogRepo, RegisteredUserLogRepo>();
            services.AddScoped<IRegisteredUserProfileRepo, RegisteredUserProfileRepo>();
            services.AddScoped<IContactDetailRepo, ContactDetailRepo>();
            services.AddScoped<IOrderHistoryManagementRepo, OrderHistoryManagementRepo>();
            services.AddScoped<IInventoryRepo, InventoryRepo>();
            services.AddScoped<IOrderRepo, OrderRepo>();
            services.AddScoped<ITransactionRepo, TransactionRepo>();

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

                // Strict: 5 attempts per 60 s — protects login and registration.
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
                options.AddPolicy("ProductList", builder => builder
                    .Expire(TimeSpan.FromMinutes(5))
                    .SetVaryByQuery("*")
                    .Tag("products"));

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

namespace ELKH.Extensions
{
    // ----- rate-limiting policy names (shared between registration and attributes) -----
    public static class RateLimitPolicies
    {
        public const string Auth     = "auth";      // login / register
        public const string Checkout = "checkout";  // payment endpoints
        public const string Search   = "search";    // autocomplete
    }
}
