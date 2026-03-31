using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using ELKH.Data;
using ELKH.Models;

namespace ELKH.Tests.Utilities;

/// <summary>
/// Base test class providing common utilities, database setup, and mocking infrastructure
/// for all test classes in the ELKH test suite.
/// </summary>
public abstract class BaseTest : IDisposable
{
    protected readonly ApplicationDbContext _context;
    protected readonly Mock<ILogger> _mockLogger;
    protected readonly string _databaseName;

    protected BaseTest()
    {
        _databaseName = $"TestDb_{Guid.NewGuid()}";
        _context = CreateInMemoryContext(_databaseName);
        _mockLogger = new Mock<ILogger>();
    }

    // ================================================================
    // Database Setup and Teardown
    // ================================================================

    /// <summary>
    /// Creates an in-memory Entity Framework context for testing
    /// </summary>
    protected static ApplicationDbContext CreateInMemoryContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .EnableSensitiveDataLogging()
            .Options;

        return new ApplicationDbContext(options);
    }

    /// <summary>
    /// Seeds the database with basic test data
    /// </summary>
    protected virtual void SeedDatabase()
    {
        // Override in derived classes to add specific test data
    }

    /// <summary>
    /// Clears all data from the test database
    /// </summary>
    protected void ClearDatabase()
    {
        _context.Database.EnsureDeleted();
        _context.Database.EnsureCreated();
    }

    // ================================================================
    // User and Authentication Mocking
    // ================================================================

    /// <summary>
    /// Creates a mock ClaimsPrincipal for authentication testing
    /// </summary>
    protected static ClaimsPrincipal CreateMockUser(
        string userId = "test-user-id",
        string email = "test@example.com",
        string[] roles = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, email)
        };

        if (roles != null)
        {
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
        }

        var identity = new ClaimsIdentity(claims, "Test");
        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Creates a mock HttpContext with authentication
    /// </summary>
    protected static HttpContext CreateMockHttpContext(ClaimsPrincipal user = null)
    {
        var context = new DefaultHttpContext();
        context.User = user ?? CreateMockUser();
        return context;
    }

    /// <summary>
    /// Sets up a controller with mock HttpContext and user authentication
    /// </summary>
    protected static T SetupControllerWithAuth<T>(T controller, ClaimsPrincipal user = null) 
        where T : ControllerBase
    {
        var httpContext = CreateMockHttpContext(user);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
        return controller;
    }

    // ================================================================
    // Service Mocking Utilities
    // ================================================================

    /// <summary>
    /// Creates a mock UserManager for Identity testing
    /// </summary>
    protected static Mock<UserManager<IdentityUser>> CreateMockUserManager()
    {
        var store = new Mock<IUserStore<IdentityUser>>();
        return new Mock<UserManager<IdentityUser>>(
            store.Object, null, null, null, null, null, null, null, null);
    }

    /// <summary>
    /// Creates a mock RoleManager for role testing
    /// </summary>
    protected static Mock<RoleManager<IdentityRole>> CreateMockRoleManager()
    {
        var store = new Mock<IRoleStore<IdentityRole>>();
        return new Mock<RoleManager<IdentityRole>>(
            store.Object, null, null, null, null);
    }

    /// <summary>
    /// Creates a mock ILogger with specific type
    /// </summary>
    protected static Mock<ILogger<T>> CreateMockLogger<T>()
    {
        return new Mock<ILogger<T>>();
    }

    // ================================================================
    // Test Data Helpers
    // ================================================================

    /// <summary>
    /// Adds a test user to the database and returns the entity
    /// </summary>
    protected RegisteredUserModel AddTestUser(
        string email = "test@example.com",
        bool saveChanges = true)
    {
        var user = TestDataFactory.CreateUser(email: email);
        _context.RegisteredUsers.Add(user);
        
        if (saveChanges)
        {
            _context.SaveChanges();
        }
        
        return user;
    }

    /// <summary>
    /// Adds a test product to the database and returns the entity
    /// </summary>
    protected ProductModel AddTestProduct(
        string name = "Test Product",
        decimal price = 19.99m,
        int stockQuantity = 100,
        bool saveChanges = true)
    {
        // First ensure we have a category
        var category = _context.Categories.FirstOrDefault() ?? AddTestCategory("Test Category", false);

        var product = TestDataFactory.CreateProduct(
            categoryId: category.PkCategoryId,
            price: price,
            stockQuantity: stockQuantity);
        product.Name = name;

        _context.Products.Add(product);

        if (saveChanges)
        {
            _context.SaveChanges();
        }

        return product;
    }

    /// <summary>
    /// Adds a test category to the database and returns the entity
    /// </summary>
    protected CategoryModel AddTestCategory(
        string name = "Test Category",
        bool saveChanges = true)
    {
        var category = TestDataFactory.CreateCategory(name: name);
        _context.Categories.Add(category);
        
        if (saveChanges)
        {
            _context.SaveChanges();
        }
        
        return category;
    }

    /// <summary>
    /// Adds a test cart item to the database
    /// </summary>
    protected CartModel AddTestCartItem(
        RegisteredUserModel user,
        ProductModel product,
        int quantity = 1,
        bool saveChanges = true)
    {
        var cartItem = TestDataFactory.CreateCartItem(
            userId: user.PkRegisteredUserId,
            productId: product.PkProductId,
            quantity: quantity);
        
        _context.Carts.Add(cartItem);
        
        if (saveChanges)
        {
            _context.SaveChanges();
        }
        
        return cartItem;
    }

    // ================================================================
    // Assertion Helpers
    // ================================================================

    /// <summary>
    /// Verifies that the mock logger was called with a specific log level
    /// </summary>
    protected void VerifyLoggerCalled<T>(Mock<ILogger<T>> logger, LogLevel level, Times times)
    {
        logger.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            times);
    }

    /// <summary>
    /// Verifies that the database contains the expected number of entities
    /// </summary>
    protected void VerifyDatabaseCount<T>(int expectedCount) where T : class
    {
        var actualCount = _context.Set<T>().Count();
        if (actualCount != expectedCount)
        {
            throw new InvalidOperationException(
                $"Expected {expectedCount} {typeof(T).Name} entities, but found {actualCount}");
        }
    }

    // ================================================================
    // Cleanup
    // ================================================================

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _context?.Dispose();
        }
    }
}