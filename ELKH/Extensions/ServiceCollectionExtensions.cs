using ELKH.Data;
using ELKH.Repositories;
using ELKH.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

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
            services.AddScoped<IRole_repo, Role_repo>();
            services.AddScoped<IOrderManagementRepo, OrderManagementRepo>();
            services.AddScoped<IRegisteredUserLogRepo, RegisteredUserLogRepo>();
            services.AddScoped<IRegisteredUserProfileRepo, RegisteredUserProfileRepo>();
            services.AddScoped<IContactDetailRepo, ContactDetailRepo>();

            return services;
        }

        /// <summary>
        /// Register email services with adapter pattern for Identity compatibility.
        /// </summary>
        public static IServiceCollection AddEmailServices(this IServiceCollection services)
        {
            // Register concrete implementations
            services.AddScoped<SmtpEmailSender>();
            services.AddScoped<EmailSenderAdapter>();
            services.AddScoped<FileEmailSender>();

            // Choose the effective IEmailSender implementation based on environment.
            // In Development, use FileEmailSender to save emails to disk rather than sending
            // them over the network. In other environments, use the EmailSenderAdapter
            // which delegates to SmtpEmailSender.
            services.AddScoped<IEmailSender>(sp =>
            {
                var env = sp.GetRequiredService<Microsoft.Extensions.Hosting.IHostEnvironment>();
                if (env.IsDevelopment()) return sp.GetRequiredService<FileEmailSender>();
                return sp.GetRequiredService<EmailSenderAdapter>();
            });

            services.AddScoped<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender>(sp =>
            {
                var env = sp.GetRequiredService<Microsoft.Extensions.Hosting.IHostEnvironment>();
                if (env.IsDevelopment()) return sp.GetRequiredService<FileEmailSender>();
                return sp.GetRequiredService<EmailSenderAdapter>();
            });
            
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
        /// Configure output caching policies for different page types.
        /// </summary>
        public static IServiceCollection AddOutputCachingPolicies(this IServiceCollection services)
        {
            services.AddOutputCache(options =>
            {
                // Default policy for most pages
                options.AddBasePolicy(builder => builder
                    .Expire(TimeSpan.FromMinutes(1))
                    .SetVaryByQuery("*"));

                // Policy for product listings - cache longer
                options.AddPolicy("ProductList", builder => builder
                    .Expire(TimeSpan.FromMinutes(5))
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
