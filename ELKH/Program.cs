using ELKH.Configuration;
using ELKH.Data;
using ELKH.Extensions;
using ELKH.Models;
using ELKH.Repositories;
using ELKH.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Versioning;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// ================= DATABASE =================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

var imageStoreConnection = builder.Configuration.GetConnectionString("ImageStoreConnection")
    ?? throw new InvalidOperationException("Connection string 'ImageStoreConnection' not found.");

builder.Services.AddDbContext<ImageStoreContext>(options =>
    options.UseSqlite(imageStoreConnection));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// ================= HEALTH =================
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("database")
    .AddDbContextCheck<ImageStoreContext>("imagestore");

// ================= SWAGGER =================
builder.Services.AddSwaggerDocumentation();

// ================= API VERSIONING =================
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
});

builder.Services.AddVersionedApiExplorer(setup =>
{
    setup.GroupNameFormat = "'v'VVV";
    setup.SubstituteApiVersionInUrl = true;
});

// ================= IDENTITY =================
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders()
.AddDefaultUI();

// ================= MVC =================
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// ================= CACHING =================
builder.Services.AddMemoryCache();
builder.Services.AddResponseCaching();

// ================= SESSION =================
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// ================= SERVICES =================
builder.Services.AddHttpClient<IPayPalService, PayPalService>();
builder.Services.AddHttpClient<IReCaptchaService, ReCaptchaService>();

builder.Services.AddScoped<ICartRepo, CartRepo>();

builder.Services.AddBackgroundServices();
builder.Services.AddApplicationServices();
builder.Services.AddEmailServices();
builder.Services.AddRepositories();

builder.Services.AddScoped<InventoryRepo>();

builder.Services.AddHttpContextAccessor();

// ================= BUILD =================
var app = builder.Build();

// ================= PIPELINE =================
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseMigrationsEndPoint();

    var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
    app.UseSwaggerDocumentation(provider);
}
else
{
    app.UseHsts();

    var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
    app.UseSwaggerDocumentation(provider);
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

// ================= MIGRATIONS =================
await using (var scope = app.Services.CreateAsyncScope())
{
    var sp = scope.ServiceProvider;

    var db = sp.GetRequiredService<ApplicationDbContext>();
    var imageDb = sp.GetRequiredService<ImageStoreContext>();

    await db.Database.MigrateAsync();
    await imageDb.Database.MigrateAsync();
}

// ================= SEEDING =================
var runSeeders = builder.Configuration.GetValue<bool>("Database:RunSeeders", true);

if (runSeeders)
{
    await using var scope = app.Services.CreateAsyncScope();
    var sp = scope.ServiceProvider;

    try
    {
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var userManager = sp.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var imageDb = sp.GetRequiredService<ImageStoreContext>();

        app.Logger.LogInformation("Starting Seeding...");

        await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=OFF");

        try
        {
            await DbSeeder.SeedProductsAsync(db);
            await DbSeeder.SeedShippingMethodsAsync(db);
            await DbSeeder.SeedUsersAndRolesAsync(userManager, roleManager, app.Configuration);
            await DbSeeder.SeedCustomersAndOrdersAsync(db, userManager, app.Environment.WebRootPath);
            await DbSeeder.SeedStoreReviewsAsync(db, userManager);
            await DbSeeder.SeedTestTransactionsAsync(db);
            await DbSeeder.SeedProductImagesAsync(db, imageDb, app.Environment.WebRootPath);

            app.Logger.LogInformation("Seeding completed successfully.");
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON");
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Seeding failed.");
    }
}

app.Run();
