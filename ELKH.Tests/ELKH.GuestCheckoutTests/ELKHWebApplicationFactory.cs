using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using ELKH.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System.Data.Common;
using ELKH.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace ELKH.GuestCheckoutTests;

/// <summary>
/// Custom web application factory for integration testing with in-memory database.
/// Provides isolated test environment with proper authentication setup.
/// </summary>
public class ELKHWebApplicationFactory : WebApplicationFactory<Program>
{
    private bool _seeded = false;
    private bool _seedingStarted = false;
    private Task? _seedingTask;
    private readonly object _seedLock = new object();
    private readonly string _appDbConnectionString = $"Data Source=GuestCheckoutAppDb_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
    private readonly string _imageDbConnectionString = $"Data Source=GuestCheckoutImageDb_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
    private readonly SqliteConnection _dbKeepAliveConnection;
    private readonly SqliteConnection _imageDbKeepAliveConnection;

    public ELKHWebApplicationFactory()
    {
        _dbKeepAliveConnection = new SqliteConnection(_appDbConnectionString);
        _imageDbKeepAliveConnection = new SqliteConnection(_imageDbConnectionString);
        _dbKeepAliveConnection.Open();
        _imageDbKeepAliveConnection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Add test configuration values to prevent validation errors
            config.AddInMemoryCollection(new Dictionary<string, string>
            {
                { "PayPal:ClientId", "test-client-id" },
                { "PayPal:Secret", "test-secret" },
                { "ReCaptcha:SiteKey", "test-site-key" },
                { "ReCaptcha:SecretKey", "test-secret-key" },
                { "Email:User", "test@example.com" },
                { "Email:Pass", "test-password" },
                { "Email:From", "test@example.com" },
                { "Seed:AdminEmail", "admin@test.com" },
                { "Seed:AdminPass", "Test123!@#" },
                { "Seed:AllowDefaultElevatedCredentials", "true" }
            }!);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<ImageStoreContext>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<DbContextOptions<ImageStoreContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ImageStoreContext>>();
            services.RemoveAll<IHostedService>();

            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlite(_appDbConnectionString);
                options.EnableSensitiveDataLogging();
            }, ServiceLifetime.Scoped);

            services.AddDbContext<ImageStoreContext>(options =>
            {
                options.UseSqlite(_imageDbConnectionString);
                options.EnableSensitiveDataLogging();
            }, ServiceLifetime.Scoped);

            services.RemoveAll<IPayPalService>();
            services.AddScoped<IPayPalService, TestPayPalService>();

            services.Configure<SessionOptions>(options =>
            {
                options.Cookie.SecurePolicy = CookieSecurePolicy.None;
            });
        });

        builder.UseEnvironment("Testing");
    }

    /// <summary>
    /// Gets the service provider and seeds the database if not already seeded.
    /// Call this from tests that need seeded data.
    /// </summary>
    public void EnsureSeeded()
    {
        Task? seedingTaskToWait;

        lock (_seedLock)
        {
            if (_seeded) return;

            if (_seedingStarted)
            {
                seedingTaskToWait = _seedingTask;
            }
            else
            {
                _seedingStarted = true;
                _seedingTask = SeedTestDataAsync();
                seedingTaskToWait = _seedingTask;
            }
        }

        seedingTaskToWait!.GetAwaiter().GetResult();
    }

    private async Task SeedTestDataAsync()
    {
        using var scope = Services.CreateScope();
        var services = scope.ServiceProvider;

        try
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            var imageDb = services.GetRequiredService<ImageStoreContext>();
            var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

            db.Database.EnsureCreated();
            imageDb.Database.EnsureCreated();

            await SeedTestData(db, userManager, roleManager);

            lock (_seedLock)
            {
                _seeded = true;
            }
        }
        catch (Exception ex)
        {
            var logger = services.GetRequiredService<ILogger<ELKHWebApplicationFactory>>();
            logger.LogError(ex, "An error occurred seeding the database with test data.");
            throw;
        }
    }

    /// <summary>
    /// Seeds the test database with required data for integration tests.
    /// </summary>
    private async Task SeedTestData(ApplicationDbContext db, UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        // Create roles
        var roles = new[] { "Admin", "Manager", "Staff", "Customer" };
        foreach (var roleName in roles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }

        // Create test users
        var testUsers = new[]
        {
            new { Email = "admin@test.com", Password = "Admin123!", Role = "Admin" },
            new { Email = "customer@test.com", Password = "Customer123!", Role = "Customer" },
            new { Email = "manager@test.com", Password = "Manager123!", Role = "Manager" }
        };

        foreach (var testUser in testUsers)
        {
            var existingUser = await userManager.FindByEmailAsync(testUser.Email);
            if (existingUser == null)
            {
                var user = new IdentityUser
                {
                    UserName = testUser.Email,
                    Email = testUser.Email,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(user, testUser.Password);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, testUser.Role);

                    // Create corresponding RegisteredUser record
                    var registeredUser = new ELKH.Models.RegisteredUserModel
                    {
                        Email = testUser.Email
                    };

                    db.RegisteredUsers.Add(registeredUser);
                }
            }
        }

        // Create test categories
        var categories = new[]
        {
            new ELKH.Models.CategoryModel { CategoryName = "Animals" },
            new ELKH.Models.CategoryModel { CategoryName = "Sports" },
            new ELKH.Models.CategoryModel { CategoryName = "Nature" }
        };

        foreach (var category in categories)
        {
            if (!db.Categories.Any(c => c.CategoryName == category.CategoryName))
            {
                db.Categories.Add(category);
            }
        }

        await db.SaveChangesAsync();

        // Create test products
        var firstCategory = db.Categories.First();
        var products = new[]
        {
            new ELKH.Models.ProductModel
            {
                Name = "Test Product 1",
                Description = "Test product for integration testing",
                Price = 9.99m,
                StockQuantity = 100,
                FkCategoryId = firstCategory.PkCategoryId,
                IsActive = true
            },
            new ELKH.Models.ProductModel
            {
                Name = "Test Product 2",
                Description = "Another test product",
                Price = 15.99m,
                StockQuantity = 50,
                FkCategoryId = firstCategory.PkCategoryId,
                IsActive = true
            }
        };

        foreach (var product in products)
        {
            if (!db.Products.Any(p => p.Name == product.Name))
            {
                db.Products.Add(product);
            }
        }

        await db.SaveChangesAsync();

        lock (_seedLock)
        {
            _seeded = true;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _dbKeepAliveConnection.Dispose();
            _imageDbKeepAliveConnection.Dispose();
        }

        base.Dispose(disposing);
    }

    private sealed class TestPayPalService : IPayPalService
    {
        public Task<string> CreateOrderAsync(decimal amount, string currency = "CAD")
        {
            return Task.FromResult($"TEST-ORDER-{Guid.NewGuid():N}");
        }

        public Task CaptureOrderAsync(string orderId)
        {
            return Task.CompletedTask;
        }

        public Task<PayPalVerificationResult> VerifyCapturedOrderAsync(string payPalOrderId, decimal expectedTotal, string expectedCurrency)
        {
            return Task.FromResult(new PayPalVerificationResult
            {
                PayPalOrderId = payPalOrderId,
                CaptureId = $"CAPTURE-{Guid.NewGuid():N}",
                Status = "COMPLETED",
                Amount = decimal.Round(expectedTotal, 2, MidpointRounding.AwayFromZero),
                Currency = expectedCurrency,
                CapturedAtUtc = DateTime.UtcNow,
                PayerId = "TEST-PAYER",
                PayerEmail = "guest@example.com",
                VerificationSummaryJson = "{}"
            });
        }
    }
}