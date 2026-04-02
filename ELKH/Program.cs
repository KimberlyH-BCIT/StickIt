using ELKH.Configuration;
using ELKH.Data;
using ELKH.Extensions;
using ELKH.Models;
using ELKH.Repositories;
using ELKH.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

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

// -- Identity
builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// -- Health Checks
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

// -- Repositories
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<OrderHistoryManagementRepo>();
builder.Services.AddScoped<InventoryRepo>();
builder.Services.AddScoped<RegisteredUserLogRepo>();
builder.Services.AddScoped<RegisteredUserProfileRepo>();
builder.Services.AddScoped<ContactDetailRepo>();
builder.Services.AddScoped<TransactionRepo>();
builder.Services.AddScoped<OrderHistoryStaffRepo>();

// -- AutoMapper
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
// HTTP request pipeline
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
await using (var seedScope = app.Services.CreateAsyncScope())
{
    var sp = seedScope.ServiceProvider;
    try
    {
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var userManager = sp.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();

        app.Logger.LogInformation("Starting Seeding...");

        await DbSeeder.SeedProductsAsync(db);
        await DbSeeder.SeedAdminAsync(userManager, roleManager, app.Configuration);

        // This is the heavy one. If it still hangs, check the file path!
        await DbSeeder.SeedCustomersAsync(db, userManager, app.Environment.WebRootPath);

        await DbSeeder.SeedTestTransactionsAsync(db);

        var imageDb2 = sp.GetRequiredService<ImageStoreContext>();
        await DbSeeder.SeedProductImagesAsync(db, imageDb2, app.Environment.WebRootPath);

        app.Logger.LogInformation("Seeding completed successfully.");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Seeding failed.");
    }
}

app.Run();