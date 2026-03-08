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

builder.Services.AddScoped<Role_repo>();
builder.Services.AddScoped<OrderManagementRepo>();

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// Register repositories for dependency injection
builder.Services.AddScoped<RegisteredUserLogRepo>();
builder.Services.AddScoped<RegisteredUserProfileRepo>();
builder.Services.AddScoped<ContactDetailRepo>();
builder.Services.AddScoped<ICartRepo, CartRepo>();

builder.Services.Configure<PayPalOptions>(builder.Configuration.GetSection("PayPal"));
builder.Services.AddHttpClient<PayPalService>();

// Team extension registrations
builder.Services.AddApplicationOptions(builder.Configuration);
builder.Services.AddBackgroundServices();
builder.Services.AddApplicationServices();
builder.Services.AddEmailServices();
builder.Services.AddRepositories();

builder.Services.AddAutoMapper(typeof(ELKH.Mapping.AutoMapperProfile));

var app = builder.Build();

/// Localization configuration - set default culture to English (Canada) and specify supported cultures and currency
var supportedCultures = new[] { new CultureInfo("en-CA") };

app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en-CA"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

//app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();