using ELKH.Data;
using ELKH.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddScoped<IRole_repo, Role_repo>();
builder.Services.AddScoped<OrderHistoryManagementRepo>();
builder.Services.AddScoped<InventoryRepo>();


builder.Services.AddScoped<OrderHistoryManagementRepo>();
builder.Services.AddScoped<InventoryRepo>();
builder.Services.AddScoped<IRole_repo, Role_repo>();


// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options =>
        options.SignIn.RequireConfirmedAccount = false)   
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddRazorPages();
builder.Services.AddControllersWithViews();

// Register repositories for dependency injection
builder.Services.AddScoped<RegisteredUserLogRepo>();
builder.Services.AddScoped<RegisteredUserProfileRepo>();
builder.Services.AddScoped<ContactDetailRepo>();
builder.Services.AddScoped<TransactionRepo>();

var app = builder.Build();

// Localization
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
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();