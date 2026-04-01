using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ELKH.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace ELKH.Tests.Integration;

/// <summary>
/// Custom web application factory for integration testing with in-memory database.
/// Provides isolated test environment with proper authentication setup.
/// </summary>
public class ELKHWebApplicationFactory : WebApplicationFactory<Program>
{
    // Shared roots ensure the seed provider and the test server use the same InMemory store.
    private readonly InMemoryDatabaseRoot _dbRoot = new();
    private readonly InMemoryDatabaseRoot _imageDbRoot = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the real database context and image store context
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));

            var imageStoreDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ImageStoreContext>));

            if (dbContextDescriptor != null)
                services.Remove(dbContextDescriptor);

            if (imageStoreDescriptor != null)
                services.Remove(imageStoreDescriptor);

            // Add in-memory databases, sharing roots so every resolved instance
            // (including any separately built provider) hits the same backing store.
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase("IntegrationTestDb", _dbRoot);
                options.EnableSensitiveDataLogging();
            });

            services.AddDbContext<ImageStoreContext>(options =>
            {
                options.UseInMemoryDatabase("IntegrationTestImageDb", _imageDbRoot);
                options.EnableSensitiveDataLogging();
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
                        Email = testUser.Email,
                        FirstName = "Test",
                        LastName = "User",
                        CreatedAt = DateTime.UtcNow
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
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }
}