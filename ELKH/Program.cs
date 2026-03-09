using ELKH.Data;
using ELKH.Extensions;
using ELKH.Models;
using ELKH.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<IRole_repo, Role_repo>();
builder.Services.AddScoped<OrderHistoryManagementRepo>();
builder.Services.AddScoped<InventoryRepo>();


builder.Services.AddScoped<OrderHistoryManagementRepo>();
builder.Services.AddScoped<InventoryRepo>();
builder.Services.AddScoped<IRole_repo, Role_repo>();
builder.Services.AddScoped<OrderHistoryManagementRepo>();
builder.Services.AddScoped<InventoryRepo>();


// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddDbContext<ImageStoreContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("ImageStoreConnection")));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// ASP.NET Core Identity with email confirmation requirement.
// Users must confirm their email before they can sign in.
// AddRoles<IdentityRole>() is required for [Authorize(Roles = "...")] to function —
// without it, role claims are never populated and role-based access always fails.
builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// -- Health Checks (for monitoring and deployment readiness)
// Exposes /health endpoint for load balancers, monitoring tools, and container orchestrators
builder.Services.AddHealthChecks();

// -- MVC / Razor
// Support for both Controllers with Views (MVC) and Razor Pages.
// This application uses both patterns for different features.
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// Configure antiforgery to also accept the validation token from the X-CSRF-TOKEN request
// header. This enables JSON AJAX endpoints (which cannot submit form-encoded token fields)
// to participate in full CSRF protection alongside standard form-based flows.
// The JavaScript in _Layout.cshtml reads the token from the csrf-token <meta> tag and
// attaches it as this header on every state-changing fetch() call.
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
});

// -- Response Compression (reduces bandwidth by ~70%)
// Compresses text-based responses (HTML, CSS, JS, JSON) using gzip/brotli.
// Enabled for HTTPS to improve performance on slow connections.
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
// Two-layer caching strategy for optimal performance:
// 1. Memory Cache - Fast in-memory storage for frequently accessed data
// 2. Output Cache - HTTP response caching with tag invalidation support
builder.Services.AddMemoryCache();
builder.Services.AddOutputCachingPolicies(); // Extension method - see ServiceCollectionExtensions.cs

// -- Configuration Options
// Binds strongly-typed configuration classes from appsettings.json sections.
// Includes: CacheOptions, SearchOptions, EmailOptions, ModerationOptions
builder.Services.AddApplicationOptions(builder.Configuration); // Extension method - see ServiceCollectionExtensions.cs

// Register repositories for dependency injection
builder.Services.AddScoped<RegisteredUserLogRepo>();
builder.Services.AddScoped<RegisteredUserProfileRepo>();
builder.Services.AddScoped<ContactDetailRepo>();
builder.Services.AddScoped<TransactionRepo>();
// -- Application Services (using extension methods for cleaner organization)
// All service registrations are grouped by functionality in extension methods.
// See Extensions/ServiceCollectionExtensions.cs for implementation details.
builder.Services.AddBackgroundServices();  // FuzzyReindexService, FuzzyHelperService
builder.Services.AddApplicationServices(); // UserService, SearchService, RatingService, ModerationService, ProductService, CartService
builder.Services.AddEmailServices();       // SmtpEmailSender, EmailSenderAdapter, IEmailSender
builder.Services.AddRepositories();        // All repository implementations with base class inheritance

// -- Mapping
// AutoMapper for DTO/ViewModel conversions. Profile defined in Mapping/AutoMapperProfile.cs
builder.Services.AddAutoMapper(typeof(ELKH.Mapping.AutoMapperProfile));

// =====================================================================
// Build application
// =====================================================================
var app = builder.Build();

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
// Automatic database migrations
// ---------------------------------------------------------------------
// PRODUCTION WARNING: Running migrations on startup is unsafe in multi-instance
// deployments. Concurrent instances racing to migrate the same database can cause
// partial schema changes, data corruption, or startup failures.
//
// Default behaviour (no config key set):
//   - Development  → migrations ARE applied automatically (good DX)
//   - Production   → migrations are SKIPPED (apply via deployment pipeline)
//
// To override, set BOTH keys in your environment:
//   Database:ApplyMigrationsOnStartup = true
//   Database:AllowMigrationInProduction = true
//
// The second key acts as an explicit acknowledgement of the production risk.
// =====================================================================
try
{
    using var scope = app.Services.CreateScope();
    var config = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
    var env = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Hosting.IHostEnvironment>();
    var logger = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>().CreateLogger("DatabaseMigration");

    var explicitlyEnabled = config.GetValue<bool?>("Database:ApplyMigrationsOnStartup");
    var productionOverride = config.GetValue<bool>("Database:AllowMigrationInProduction");

    // Determine whether to apply migrations:
    // - Development: apply by default unless explicitly disabled
    // - Production:  only apply if BOTH opt-in keys are set to true
    bool apply;
    if (env.IsDevelopment())
    {
        apply = explicitlyEnabled ?? true;
    }
    else
    {
        apply = explicitlyEnabled == true && productionOverride;
        if (apply)
        {
            logger.LogWarning(
                "Applying EF Core migrations on startup in a non-Development environment. " +
                "This is unsafe in multi-instance deployments and should be moved to the " +
                "deployment pipeline. Set Database:AllowMigrationInProduction=false to disable.");
        }
    }

    if (apply)
    {
        try
        {
            logger.LogInformation("Applying pending Entity Framework Core migrations...");
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.Migrate();

            // Ensure UserActivityLog columns exist (idempotent — SQLite throws if column
            // already exists, so the exception is swallowed and execution continues).
            foreach (var sql in new[]
            {
                "ALTER TABLE UserLogs ADD COLUMN ActivityType TEXT NULL",
                "ALTER TABLE UserLogs ADD COLUMN ActivityDetail TEXT NULL",
                "ALTER TABLE UserProfiles ADD COLUMN AvatarData BLOB NULL",
                "ALTER TABLE UserProfiles ADD COLUMN AvatarMimeType TEXT NULL"
            })
            {
                try { db.Database.ExecuteSqlRaw(sql); }
                catch { /* column already present — no action needed */ }
            }

            logger.LogInformation("Database migrations applied successfully.");

            // Seed demo products (no-op if products already exist)
            await DbSeeder.SeedProductsAsync(db);

            // Seed 50 demo customer accounts (no-op if any @home.com users exist)
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var webEnv  = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
            await DbSeeder.SeedCustomersAsync(db, userMgr, webEnv.WebRootPath);

            // Seed default admin role and test admin account (no-op if already exists).
            // Credentials are read from config — set Seed:AdminEmail and Seed:AdminPass
            // via user-secrets in development or environment variables in production.
            var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            await DbSeeder.SeedAdminAsync(userMgr, roleMgr, config);

            logger.LogInformation("Database seeding complete.");
        }
        catch (Exception ex)
        {
            // Log but do not prevent the application from starting.
            var logger2 = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Program>>();
            logger2.LogError(ex, "An error occurred while applying database migrations on startup.");
        }
    }
}
catch (Exception ex)
{
    // Use a local LoggerFactory instead of building a second DI container, which
    // would produce a "service provider already built" warning and bypass singleton
    // lifetime management.
    using var fallbackFactory = LoggerFactory.Create(b => b.AddConsole());
    var logger = fallbackFactory.CreateLogger("DatabaseMigrationStart");
    logger.LogWarning(ex, "Failed to initialize automatic migration application logic.");
}

// =====================================================================
// HTTP request pipeline
// ---------------------------------------------------------------------
// Middleware order is critical in ASP.NET Core. This pipeline is carefully
// ordered for optimal security, performance, and functionality.
// 
// Order: Exception Handling → HTTPS → Compression → Caching → Routing 
//        → Authentication → Authorization → Endpoints
// =====================================================================
// Localization
var supportedCultures = new[] { new CultureInfo("en-CA") };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en-CA"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

// -- Environment-specific error handling
if (app.Environment.IsDevelopment())
{
    // Development: Show detailed error pages with stack traces and database queries
    app.UseMigrationsEndPoint();
}
else
{
    // Production: User-friendly error page without sensitive information
    app.UseExceptionHandler("/Home/Error");
    // HSTS: Enforce HTTPS for 30 days (prevents downgrade attacks)
    app.UseHsts();
}

app.UseHttpsRedirection();   
app.UseStaticFiles();        
app.UseRouting();            
app.UseAuthentication();
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// ─── Security headers ────────────────────────────────────────────────────────
// Applied before static files so even asset responses carry the headers.
//
//  X-Content-Type-Options: nosniff
//      Prevents browsers from MIME-sniffing a response away from the declared
//      Content-Type (e.g. treating a text/plain upload as text/html).
//
//  X-Frame-Options: SAMEORIGIN
//      Blocks the application from being embedded in a cross-origin <iframe>,
//      mitigating clickjacking attacks. SAMEORIGIN permits same-site embedding
//      (e.g. admin sub-pages within the same domain) while blocking external frames.
//
//  Referrer-Policy: strict-origin-when-cross-origin
//      Sends the full URL as referrer for same-origin requests, but only the
//      origin (no path/query) for cross-origin requests, preventing sensitive
//      URL parameters (order IDs, tokens) from leaking to third-party sites.
//
//  X-Permitted-Cross-Domain-Policies: none
//      Stops Adobe Flash and Acrobat from loading data from this domain.
//      Included for defence-in-depth even though Flash is retired.
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options",          "nosniff");
    context.Response.Headers.Append("X-Frame-Options",                 "SAMEORIGIN");
    context.Response.Headers.Append("Referrer-Policy",                 "strict-origin-when-cross-origin");
    context.Response.Headers.Append("X-Permitted-Cross-Domain-Policies", "none");
    await next();
});

app.UseStaticFiles();

// Extension method configures the standard middleware stack in correct order:
// Response Compression, Response/Output Caching, Routing, Authentication, Authorization.
// See Extensions/ApplicationBuilderExtensions.cs for implementation.
app.UseApplicationMiddleware();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// =====================================================================
// Routing and endpoints
// ---------------------------------------------------------------------
// Map all application endpoints (controllers, Razor pages, health checks).
// Extension method ensures consistent endpoint configuration.
// 
// Endpoints configured:
// - Static assets (CSS, JS, images with caching headers)
// - Controller routes (default pattern: {controller=Home}/{action=Index}/{id?})
// - Razor Pages (convention-based routing)
// - Health checks (/health endpoint for monitoring)
// 
// See Extensions/ApplicationBuilderExtensions.cs for implementation
// =====================================================================
app.UseApplicationEndpoints();

app.Run();