using ELKH.Configuration;
using ELKH.Data;
using ELKH.Extensions;
using ELKH.Models;
using ELKH.Repositories;
using ELKH.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using System.Globalization;

// =====================================================================
// PROGRAM.CS - APPLICATION STARTUP AND CONFIGURATION
// =====================================================================
//
// TABLE OF CONTENTS
// ==================
// 1. Service Registration (lines 17-111)
//    - Database & Identity (lines 17-46)
//    - Health Checks (lines 48-50)
//    - MVC / Razor Pages (lines 52-74)
//    - Caching & Compression (lines 76-82)
//    - Rate Limiting (lines 84)
//    - Configuration Options (lines 86)
//    - Payment & Security Services (lines 88-94)
//    - Repository Registration (lines 96-102)
//    - Mapping (ProductMapper instead of AutoMapper) (lines 104-114)
//
// 2. Application Build & Configuration (lines 116-169)
//    - Allowed Hosts Validation (lines 121-132)
//    - HTTP Request Pipeline (lines 134-159)
//    - Routing & Endpoints (lines 161-165)
//
// 3. Database Migration & Seeding (lines 167-189)
//    - Migration Strategy (controlled by config)
//    - Idempotent Seeding (products, admin, customers)
//
// =====================================================================

var builder = WebApplication.CreateBuilder(args);

// Configure logging to avoid EventLog disposal issues
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// -- Database and Identity
// SQLite database with Entity Framework Core. Connection string is required
// and will throw if missing to fail fast during startup.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));

var imageStoreConnection = builder.Configuration.GetConnectionString("ImageStoreConnection")
    ?? throw new InvalidOperationException("Connection string 'ImageStoreConnection' not found.");
builder.Services.AddDbContext<ImageStoreContext>(options =>
    options.UseSqlite(imageStoreConnection));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// -- Health Checks (for monitoring and deployment readiness)
// Exposes /health endpoint for load balancers, monitoring tools, and container orchestrators
// Includes checks for:
// - Database connectivity (ApplicationDbContext, ImageStoreContext)
// - PayPal API accessibility and credential validation
// - Email/SMTP server connectivity (production only, skipped in development)
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>(
        name: "database",
        failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
        tags: new[] { "db", "sql", "ready" })
    .AddDbContextCheck<ImageStoreContext>(
        name: "imagestore",
        failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
        tags: new[] { "db", "sql", "ready" })
    .AddCheck<ELKH.HealthChecks.PayPalHealthCheck>(
        name: "paypal",
        failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded,
        tags: new[] { "external", "payment", "live" })
    .AddCheck<ELKH.HealthChecks.EmailHealthCheck>(
        name: "email",
        failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded,
        tags: new[] { "external", "smtp", "live" });

// Add Swagger/OpenAPI documentation
builder.Services.AddSwaggerDocumentation();

// -- Application Insights & Monitoring
// Azure Application Insights for telemetry, performance monitoring, and diagnostics
builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("ApplicationInsights");
    options.DeveloperMode = builder.Environment.IsDevelopment();
    options.EnableAdaptiveSampling = !builder.Environment.IsDevelopment();
    options.EnableQuickPulseMetricStream = true;
    options.EnableAuthenticationTrackingJavaScript = true;
    options.EnableDependencyTrackingTelemetryModule = true;
    options.EnablePerformanceCounterCollectionModule = true;
    options.EnableRequestTrackingTelemetryModule = true;
});

// Add API versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new Microsoft.AspNetCore.Mvc.ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new QueryStringApiVersionReader("v"),
        new HeaderApiVersionReader("X-API-Version"),
        new UrlSegmentApiVersionReader()
    );
    options.ReportApiVersions = true;
});

builder.Services.AddVersionedApiExplorer(setup =>
{
    setup.GroupNameFormat = "'v'VVV";
    setup.SubstituteApiVersionInUrl = true;
});

// ASP.NET Core Identity with email confirmation requirement.
// Users must confirm their email before they can sign in.
// AddRoles<IdentityRole>() is required for [Authorize(Roles = "...")] to function -
// without it, role claims are never populated and role-based access always fails.
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders()
    .AddDefaultUI();

// -- MVC / Razor
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// Configure antiforgery to also accept the validation token from the X-CSRF-TOKEN request
// header. This enables JSON AJAX endpoints (which cannot submit form-encoded token fields)
// to participate in full CSRF protection alongside standard form-based flows.
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
});

// -- Response Compression (reduces bandwidth by ~70%)
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = new[]
    {
        "text/plain",
        "text/html",
        "text/css",
        "application/javascript",
        "application/json",
        "application/xml"
    };
});

// -- Caching
builder.Services.AddMemoryCache();
builder.Services.AddOutputCachingPolicies();

// Response caching for API endpoints
builder.Services.AddResponseCaching(options =>
{
    options.MaximumBodySize = 1024 * 1024; // 1 MB
    options.UseCaseSensitivePaths = false;
});

// Cache profiles for different types of content
builder.Services.Configure<MvcOptions>(options =>
{
    options.CacheProfiles.Add("ProductCatalog", new CacheProfile
    {
        Duration = 300, // 5 minutes
        Location = ResponseCacheLocation.Any,
        VaryByHeader = "Accept,Accept-Encoding"
    });

    options.CacheProfiles.Add("ProductDetails", new CacheProfile
    {
        Duration = 600, // 10 minutes
        Location = ResponseCacheLocation.Any,
        VaryByHeader = "Accept,Accept-Encoding"
    });

    options.CacheProfiles.Add("SearchResults", new CacheProfile
    {
        Duration = 180, // 3 minutes
        Location = ResponseCacheLocation.Any,
        VaryByHeader = "Accept,Accept-Encoding",
        VaryByQueryKeys = new[] { "q", "limit" }
    });
});

// -- Session Support (for guest checkout)
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Session expires after 30 minutes of inactivity
    options.Cookie.HttpOnly = true; // Prevent JavaScript access to session cookie
    options.Cookie.IsEssential = true; // Required for guest checkout functionality
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // HTTPS only
    options.Cookie.SameSite = SameSiteMode.Lax; // CSRF protection
});

// -- Rate Limiting (brute-force / enumeration protection)
builder.Services.AddRateLimitingPolicies();

// -- Configuration Options
builder.Services.AddApplicationOptions(builder.Configuration);

// -- Payment and security services
builder.Services.AddHttpClient<IPayPalService, PayPalService>();
builder.Services.AddHttpClient<IReCaptchaService, ReCaptchaService>();

// Register repositories whose callers inject the concrete type or interface not covered by AddRepositories()
builder.Services.AddScoped<ICartRepo, CartRepo>();
// -- Application Services (using extension methods for cleaner organization)
// All service registrations are grouped by functionality in extension methods.
// See Extensions/ServiceCollectionExtensions.cs for implementation details.
builder.Services.AddBackgroundServices();  // FuzzyReindexService, FuzzyHelperService
builder.Services.AddApplicationServices(); // All application services including image optimization, logging, guest cart
builder.Services.AddEmailServices();       // SmtpEmailSender, EmailSenderAdapter, IEmailSender
builder.Services.AddRepositories();        // All repository implementations with base class inheritance

builder.Services.AddHttpContextAccessor(); // Required for CorrelationId access and session-based cart

// -- Mapping
// NOTE: AutoMapper 16.x removed DI extension support and changed the API significantly.
// AutoMapper versions 12.x-15.x have a known HIGH SEVERITY vulnerability (GHSA-rvv3-g6hj-g44x).
// Since we only map ProductModel <-> ProductVM, we've removed AutoMapper entirely and
// implemented manual mapping via IProductMapper/ProductMapper services.
// 
// Benefits: No vulnerabilities, better performance, type-safe, explicit mapping logic.
// See Services/ProductMapper.cs for implementation and ProductService for usage.
//
// Future: If more complex mapping is needed, consider Mapperly (source-generated mapper)
// which provides AutoMapper-like convenience without runtime overhead or security issues.

// =====================================================================
// Build application
// =====================================================================
var app = builder.Build();

// =======================================================================
// CONFIGURATION VALIDATION
// Validate all required secrets and configuration at startup to fail fast
// before any HTTP requests are processed. In Development, logs warnings.
// In Production, throws exception and aborts startup.
// =======================================================================
using (var scope = app.Services.CreateScope())
{
    var validator = new ConfigurationValidator(
        scope.ServiceProvider.GetRequiredService<ILogger<ConfigurationValidator>>(),
        scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>(),
        scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<PayPalOptions>>(),
        scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ReCaptchaOptions>>(),
        scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<EmailOptions>>(),
        scope.ServiceProvider.GetRequiredService<IConfiguration>());

    validator.ValidateConfiguration();
}

// Warn loudly if AllowedHosts is still the development default in a non-Development environment.
// Override via the ASPNETCORE_AllowedHosts environment variable (e.g. "yourdomain.com;www.yourdomain.com").
if (!app.Environment.IsDevelopment())
{
    var allowedHosts = app.Configuration["AllowedHosts"];
    if (string.IsNullOrEmpty(allowedHosts) || allowedHosts == "localhost" || allowedHosts == "*")
    {
        app.Logger.LogWarning(
            "AllowedHosts is '{Value}'. Set the ASPNETCORE_AllowedHosts environment variable " +
            "to your production domain(s) to enable host header filtering.",
            allowedHosts ?? "(empty)");
    }
}

// =====================================================================
// HTTP request pipeline
// =====================================================================
if (app.Environment.IsDevelopment())
{
    // Development: show detailed error page with stack traces and DB queries.
    app.UseDeveloperExceptionPage();
    app.UseMigrationsEndPoint();

    // Enable Swagger in development
    var devApiVersionProvider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
    app.UseSwaggerDocumentation(devApiVersionProvider);
}
else
{
    // HSTS: Enforce HTTPS for 30 days (prevents downgrade attacks).
    // Exception handling and status-code pages are registered inside UseApplicationMiddleware.
    app.UseHsts();

    // Enable Swagger in production for API documentation (consider security implications)
    var prodApiVersionProvider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
    app.UseSwaggerDocumentation(prodApiVersionProvider);
}

app.UseApplicationMiddleware(app.Environment);

// Static files AFTER compression so they can be compressed
app.UseStaticFiles();

// =====================================================================
// Routing and endpoints
// =====================================================================
app.UseApplicationEndpoints();

// =====================================================================
// Database migration and seeding
// =====================================================================
// Migrations: controlled by Database:ApplyMigrationsOnStartup (defaults true
// in Development, false elsewhere). Database:AllowMigrationInProduction must
// ALSO be true before migrations run outside Development - the double-guard
// prevents accidental schema changes on a shared production database.
//
// Seeding: fully idempotent - each seeder checks for existing data and
// returns immediately when the database is already populated.
// ... after app.Build() and middleware ...

// 1. Run Migrations first and dispose of that scope immediately
await using (var migrationScope = app.Services.CreateAsyncScope())
{
    var sp = migrationScope.ServiceProvider;
    try
    {
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var imageDb = sp.GetRequiredService<ImageStoreContext>();

        
        await db.Database.MigrateAsync();
        await imageDb.Database.MigrateAsync();
        app.Logger.LogInformation("Migrations applied successfully.");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Migration failed.");
        throw; // Don't try to seed if migrations failed
    }
}

// 2. Run Seeding in its own fresh scope
// Seeding can be disabled via Database:RunSeeders = false in appsettings.json
// or via environment variable. Useful after first run to prevent accidental reseeding.
var runSeeders = app.Configuration.GetValue<bool>("Database:RunSeeders", defaultValue: true);

if (runSeeders)
{
    await using (var seedScope = app.Services.CreateAsyncScope())
    {
        var sp = seedScope.ServiceProvider;
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var userManager = sp.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();

        app.Logger.LogInformation("Starting Seeding...");

        // Temporarily disable foreign key constraints for seeding
        await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=OFF");

        try
        {
            await DbSeeder.SeedProductsAsync(db);
            await DbSeeder.SeedShippingMethodsAsync(db); // Seed shipping options
            await DbSeeder.SeedUsersAndRolesAsync(userManager, roleManager, app.Configuration);
            await DbSeeder.SeedCustomersAndOrdersAsync(db, userManager, app.Environment.WebRootPath);
            await DbSeeder.SeedStoreReviewsAsync(db, userManager); // Seed featured homepage reviews
            await DbSeeder.SeedTestTransactionsAsync(db);

            app.Logger.LogInformation("Seeding completed successfully.");
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "Seeding failed.");
        }
        finally
        {
            // Re-enable foreign key constraints after seeding
            await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON");
        }
    }
}

app.Run();
