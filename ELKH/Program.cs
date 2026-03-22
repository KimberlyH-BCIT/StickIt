using ELKH.Configuration;
using ELKH.Data;
using ELKH.Extensions;
using ELKH.Models;
using ELKH.Repositories;
using ELKH.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.UI.Services;

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

// ASP.NET Core Identity with email confirmation requirement.
// Users must confirm their email before they can sign in.
// AddRoles<IdentityRole>() is required for [Authorize(Roles = "...")] to function —
// without it, role claims are never populated and role-based access always fails.
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders()
    .AddDefaultUI();

// -- Health Checks (for monitoring and deployment readiness)
// Exposes /health endpoint for load balancers, monitoring tools, and container orchestrators
builder.Services.AddHealthChecks();

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

// -- Rate Limiting (brute-force / enumeration protection)
builder.Services.AddRateLimitingPolicies();

// -- Configuration Options
builder.Services.AddApplicationOptions(builder.Configuration);

// -- Payment and security services
builder.Services.Configure<PayPalOptions>(builder.Configuration.GetSection("PayPal"));
builder.Services.AddHttpClient<IPayPalService, PayPalService>();
builder.Services.Configure<ReCaptchaOptions>(builder.Configuration.GetSection("ReCaptcha"));
builder.Services.AddHttpClient<IReCaptchaService, ReCaptchaService>();

// Register repositories whose callers inject the concrete type or interface not covered by AddRepositories()
builder.Services.AddScoped<ICartRepo, CartRepo>();
// -- Application Services (using extension methods for cleaner organization)
// All service registrations are grouped by functionality in extension methods.
// See Extensions/ServiceCollectionExtensions.cs for implementation details.
builder.Services.AddBackgroundServices();  // FuzzyReindexService, FuzzyHelperService
builder.Services.AddApplicationServices(); // UserService, SearchService, RatingService, ModerationService, ProductService, CartService
builder.Services.AddEmailServices();       // SmtpEmailSender, EmailSenderAdapter, IEmailSender
builder.Services.AddRepositories();        // All repository implementations with base class inheritance



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
// ---------------------------------------------------------------------
// Middleware order is critical in ASP.NET Core. This pipeline is carefully
// ordered for optimal security, performance, and functionality.
// 
// Order: Exception Handling → HTTPS → Compression → Caching → Routing 
//        → Authentication → Authorization → Endpoints
// =====================================================================
if (app.Environment.IsDevelopment())
{
    // Development: show detailed error page with stack traces and DB queries.
    app.UseDeveloperExceptionPage();
    app.UseMigrationsEndPoint();
}
else
{
    // HSTS: Enforce HTTPS for 30 days (prevents downgrade attacks).
    // Exception handling and status-code pages are registered inside UseApplicationMiddleware.
    app.UseHsts();
}

app.UseStaticFiles();

app.UseApplicationMiddleware();

// =====================================================================
// Routing and endpoints
// =====================================================================
app.UseApplicationEndpoints();

// =====================================================================
// Database migration and seeding
// =====================================================================
// Migrations: controlled by Database:ApplyMigrationsOnStartup (defaults true
// in Development, false elsewhere). Database:AllowMigrationInProduction must
// ALSO be true before migrations run outside Development — the double-guard
// prevents accidental schema changes on a shared production database.
//
// Seeding: fully idempotent — each seeder checks for existing data and
// returns immediately when the database is already populated.
await using (var scope = app.Services.CreateAsyncScope())
{
    var sp = scope.ServiceProvider;
    var db = sp.GetRequiredService<ApplicationDbContext>();

    var applyMigrations     = app.Configuration.GetValue<bool?>("Database:ApplyMigrationsOnStartup")
                                  ?? app.Environment.IsDevelopment();
    var allowInProduction   = app.Configuration.GetValue<bool>("Database:AllowMigrationInProduction");

    if (applyMigrations && (app.Environment.IsDevelopment() || allowInProduction))
    {
        await db.Database.MigrateAsync();
        await sp.GetRequiredService<ImageStoreContext>().Database.MigrateAsync();
    }

    // Seeding can be disabled via Database:RunSeeders = false in appsettings.json
    // or via environment variable. Useful after first run to prevent accidental reseeding.
    var runSeeders = app.Configuration.GetValue<bool>("Database:RunSeeders", defaultValue: true);

    if (runSeeders)
    {
        var userManager = sp.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();

        await DbSeeder.SeedProductsAsync(db);
        await DbSeeder.SeedAdminAsync(userManager, roleManager, app.Configuration);
        await DbSeeder.SeedCustomersAsync(db, userManager, app.Environment.WebRootPath);
    }
}

app.Run();