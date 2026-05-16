using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using ELKH.Data;
using ELKH.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Globalization;
using System.Text;

namespace ELKH.Tests.Integration;

/// <summary>
/// Custom web application factory for integration testing with in-memory database.
/// Provides isolated test environment with proper authentication setup.
/// </summary>
public class ELKHWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _appDbConnectionString = $"Data Source=ELKHIntegrationAppDb_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
    private readonly string _imageDbConnectionString = $"Data Source=ELKHIntegrationImageDb_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

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
        builder.ConfigureServices(services =>
        {
            // Remove the real database registrations so the test host does not end up
            // with both SQLite and InMemory providers attached to the same DbContext.
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<ImageStoreContext>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<DbContextOptions<ImageStoreContext>>();
            services.RemoveAll<IHostedService>();

            var dbContextConfigurationDescriptors = services
                .Where(d => d.ServiceType.IsGenericType
                            && d.ServiceType.Name.StartsWith("IDbContextOptionsConfiguration", StringComparison.Ordinal)
                            && (d.ServiceType.GenericTypeArguments[0] == typeof(ApplicationDbContext)
                                || d.ServiceType.GenericTypeArguments[0] == typeof(ImageStoreContext)))
                .ToList();

            foreach (var descriptor in dbContextConfigurationDescriptors)
            {
                services.Remove(descriptor);
            }

            // Use shared SQLite in-memory connections so the app runs against a single
            // relational provider during tests and avoids mixed-provider startup conflicts.
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlite(_appDbConnectionString);
                options.EnableSensitiveDataLogging();
            });

            services.AddDbContext<ImageStoreContext>(options =>
            {
                options.UseSqlite(_imageDbConnectionString);
                options.EnableSensitiveDataLogging();
            });
        });

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string>
            {
                { "Seed:AllowDefaultElevatedCredentials", "true" }
            });
        });

        builder.UseEnvironment("Testing");
    }

    // Seed after the host is fully built so we use the host's own IServiceProvider,
    // guaranteeing we write into the same InMemory store the test server will use.
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var scopedServices = scope.ServiceProvider;
        var db = scopedServices.GetRequiredService<ApplicationDbContext>();
        var imageDb = scopedServices.GetRequiredService<ImageStoreContext>();
        var userManager = scopedServices.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = scopedServices.GetRequiredService<RoleManager<IdentityRole>>();
        var logger = scopedServices.GetRequiredService<ILogger<ELKHWebApplicationFactory>>();

        db.Database.EnsureCreated();
        imageDb.Database.EnsureCreated();

        try
        {
            SeedTestData(db, userManager, roleManager).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred seeding the database with test data.");
        }

        return host;
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
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }

        // Create test users
        var testUsers = new[]
        {
            new { Email = "admin@test.com", Password = "Admin123!", Role = "Admin" },
            new { Email = "customer@test.com", Password = "Customer123!", Role = "Customer" },
            new { Email = "manager@test.com", Password = "Manager123!", Role = "Manager" }
        };

        var registeredUsers = new List<RegisteredUserModel>();

        foreach (var testUser in testUsers)
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
                registeredUsers.Add(new RegisteredUserModel
                {
                    Email = testUser.Email
                });
            }
        }

        // Create test categories
        var categories = new[]
        {
            new ELKH.Models.CategoryModel { CategoryName = "Animals" },
            new ELKH.Models.CategoryModel { CategoryName = "Sports" },
            new ELKH.Models.CategoryModel { CategoryName = "Nature" }
        };

        db.RegisteredUsers.AddRange(registeredUsers);
        db.Categories.AddRange(categories);

        // Create test products
        var firstCategory = categories[0];
        var products = new[]
        {
            new ELKH.Models.ProductModel
            {
                Name = "Test Product 1",
                Description = "Test product for integration testing",
                Price = 9.99m,
                StockQuantity = 100,
                Category = firstCategory,
                IsActive = true
            },
            new ELKH.Models.ProductModel
            {
                Name = "Test Product 2",
                Description = "Another test product",
                Price = 15.99m,
                StockQuantity = 50,
                Category = firstCategory,
                IsActive = true
            }
        };

        foreach (var product in products)
        {
            product.NameNormalized = NormalizeName(product.Name);
            product.Tags = "test,integration";
        }

        db.Products.AddRange(products);
        await db.SaveChangesAsync();

        var fuzzySuggestions = new List<FuzzySuggestionModel>(products.Length);

        foreach (var product in products)
        {
            fuzzySuggestions.Add(new FuzzySuggestionModel
            {
                PkProductId = product.PkProductId,
                Name = product.Name,
                NameNormalized = product.NameNormalized,
                Price = product.Price,
                Thumbnail = string.Empty
            });
        }

        db.FuzzySuggestions.AddRange(fuzzySuggestions);
        await db.SaveChangesAsync();
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

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;

        var normalized = name.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();

        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch);
            }
        }

        return builder
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .ToLowerInvariant();
    }
}