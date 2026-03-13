using ELKH.Data;
using ELKH.Extensions;
using ELKH.Models;
using ELKH.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// -- Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddDbContext<ImageStoreContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("ImageStoreConnection")));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// -- Identity
builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// -- Health Checks
builder.Services.AddHealthChecks();

// -- MVC / Razor Pages
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// -- Antiforgery (CSRF)
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
});

// -- Response Compression
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

// -- Configuration Options
builder.Services.AddApplicationOptions(builder.Configuration);

// -- Application Services
builder.Services.AddBackgroundServices();
builder.Services.AddApplicationServices();
builder.Services.AddEmailServices();
builder.Services.AddRepositories();

// -- Repositories
builder.Services.AddScoped<IRole_repo, Role_repo>();
builder.Services.AddScoped<OrderHistoryManagementRepo>();
builder.Services.AddScoped<InventoryRepo>();
builder.Services.AddScoped<RegisteredUserLogRepo>();
builder.Services.AddScoped<RegisteredUserProfileRepo>();
builder.Services.AddScoped<ContactDetailRepo>();
builder.Services.AddScoped<TransactionRepo>();

// -- AutoMapper
builder.Services.AddAutoMapper(typeof(ELKH.Mapping.AutoMapperProfile));

// =====================================================================
// Build application
// =====================================================================
var app = builder.Build();

// -- Automatic database migrations
try
{
    using var scope = app.Services.CreateScope();
    var config = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
    var env = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Hosting.IHostEnvironment>();
    var logger = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>().CreateLogger("DatabaseMigration");

    var explicitlyEnabled = config.GetValue<bool?>("Database:ApplyMigrationsOnStartup");
    var productionOverride = config.GetValue<bool>("Database:AllowMigrationInProduction");

    bool apply = env.IsDevelopment()
        ? (explicitlyEnabled ?? true)
        : (explicitlyEnabled == true && productionOverride);

    if (apply)
    {
        logger.LogInformation("Applying pending Entity Framework Core migrations...");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.Migrate();

        foreach (var sql in new[]
        {
            "ALTER TABLE UserLogs ADD COLUMN ActivityType TEXT NULL",
            "ALTER TABLE UserLogs ADD COLUMN ActivityDetail TEXT NULL",
            "ALTER TABLE UserProfiles ADD COLUMN AvatarData BLOB NULL",
            "ALTER TABLE UserProfiles ADD COLUMN AvatarMimeType TEXT NULL"
        })
        {
            try { db.Database.ExecuteSqlRaw(sql); }
            catch { /* column already present */ }
        }

        logger.LogInformation("Database migrations applied successfully.");

        await DbSeeder.SeedProductsAsync(db);

        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var webEnv = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
        await DbSeeder.SeedCustomersAsync(db, userMgr, webEnv.WebRootPath);

        var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        await DbSeeder.SeedAdminAsync(userMgr, roleMgr, config);

        logger.LogInformation("Database seeding complete.");
    }
}
catch (Exception ex)
{
    using var fallbackFactory = LoggerFactory.Create(b => b.AddConsole());
    var logger = fallbackFactory.CreateLogger("DatabaseMigration");
    logger.LogWarning(ex, "Failed to apply database migrations on startup.");
}

// =====================================================================
// HTTP request pipeline
// =====================================================================

var supportedCultures = new[] { new CultureInfo("en-CA") };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en-CA"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseSecurityHeaders();
app.UseHttpsRedirection();
app.UseResponseCompression();
app.UseOutputCache();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

app.MapHealthChecks("/health");

app.Run();