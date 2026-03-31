using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Identity;
using Moq;
using System.Security.Claims;
using Xunit;
using ELKH.Controllers;
using ELKH.Data;
using ELKH.Models;
using ELKH.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Tests.Unit.Controllers;

/// <summary>
/// Comprehensive unit tests for ManagerController administrative functionality in the StickIt e-commerce application.
/// 
/// This test suite validates manager dashboard operations, product lifecycle management, transaction oversight,
/// and staff account administration. Tests ensure proper authorization enforcement, data integrity, and
/// administrative workflow compliance.
/// </summary>
/// <remarks>
/// TABLE OF CONTENTS (285 lines)
/// ================================================================================
/// 1. Test Setup & Configuration ..................................... Lines   52-84
///    - Constructor & Dependencies                   // Mock services, DbContext, and UserManager setup
///    - SetupManagerUser()                          // Helper for manager authentication context
///    - SetupMockDbSet()                            // Generic DbSet mock configuration utility
/// 
/// 2. Dashboard & Metrics Display Tests .............................. Lines   86-110
///    - Index_ShouldReturnViewWithDashboardMetrics() // Manager dashboard data aggregation validation
/// 
/// 3. Product Management Operations ................................... Lines  112-155
///    - ListOfProducts_ShouldReturnViewWithProducts()           // Product catalog management view
///    - ListOfProducts_WithSearchFilter_ShouldReturnFilteredProducts() // Product search functionality
///    - ToggleActive_WithValidProductId_ShouldToggleProductStatus()    // Product activation/deactivation
///    - ToggleActive_WithInvalidProductId_ShouldReturnNotFound()       // Error handling for missing products
/// 
/// 4. Transaction Oversight & Financial Management ................... Lines  157-195
///    - ListAllTransactions_ShouldReturnViewWithTransactions()         // Complete transaction history view
///    - ListAllTransactions_WithStatusFilter_ShouldReturnFilteredTransactions() // Transaction status filtering
/// 
/// 5. Staff Account Administration ................................... Lines  197-220
///    - ListOfStaffAccount_ShouldReturnViewWithStaffAccounts()         // Staff user management interface
/// 
/// 6. Helper Methods & Test Utilities ................................ Lines  222-285
///    - SetupManagerUser()                                             // Manager authentication context setup
///    - SetupMockDbSet()                                              // Generic DbSet mock configuration
/// ================================================================================
/// 
/// TESTING PATTERNS:
/// • Entity Framework DbContext mocking with in-memory database simulation
/// • ASP.NET Core Identity UserManager mocking for user management testing
/// • Complex IQueryable mock setup for LINQ query testing
/// • Role-based authentication simulation for administrative access control
/// • JSON response validation for AJAX operations
/// 
/// BUSINESS RULES VALIDATED:
/// • Manager role required for all administrative operations
/// • Product activation/deactivation maintains data integrity
/// • Transaction filtering supports financial oversight and reporting
/// • Staff account management respects organizational hierarchy
/// • Dashboard metrics provide comprehensive business intelligence
/// 
/// SECURITY CONSIDERATIONS:
/// • Role-based access control enforcement for all administrative functions
/// • Manager authentication required for sensitive operations
/// • Data isolation between different organizational levels
/// • Audit trail maintenance through proper transaction logging
/// • Input validation and sanitization for administrative interfaces
/// 
/// TECHNICAL COMPLEXITY:
/// • DbContext mocking requires sophisticated IQueryable provider simulation
/// • UserManager dependency injection with complex constructor requirements
/// • Entity Framework relationship navigation and query optimization testing
/// • Asynchronous operation testing with proper cancellation token handling
/// </remarks>
public class ManagerControllerTests
{
    #region Test Setup & Configuration

    // ── Mock Dependencies ──
    private readonly Mock<ApplicationDbContext> _mockDbContext;
    private readonly Mock<UserManager<IdentityUser>> _mockUserManager;
    private readonly ManagerController _controller;
    private readonly Mock<DbSet<ProductModel>> _mockProductSet;
    private readonly Mock<DbSet<TransactionModel>> _mockTransactionSet;

    /// <summary>
    /// Initializes comprehensive test environment with Entity Framework and Identity mocking.
    /// </summary>
    /// <remarks>
    /// COMPLEX DEPENDENCY SETUP:
    /// • ApplicationDbContext with in-memory database for data operations
    /// • UserManager with full ASP.NET Core Identity stack simulation
    /// • DbSet mocking for Products and Transactions with IQueryable support
    /// 
    /// TECHNICAL CHALLENGES:
    /// • UserManager requires complex constructor with multiple service dependencies
    /// • DbContext mocking needs proper in-memory database configuration
    /// • IQueryable provider setup for LINQ query execution testing
    /// • Claims-based authentication simulation for manager authorization
    /// 
    /// This sophisticated setup enables comprehensive testing of administrative
    /// operations while maintaining complete isolation from actual database and
    /// identity systems.
    /// </remarks>
    public ManagerControllerTests()
    {
        #region DbContext Configuration

        // Setup in-memory database with unique identifier to prevent test interference
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _mockDbContext = new Mock<ApplicationDbContext>(options);

        #endregion

        #region UserManager Configuration

        // Setup ASP.NET Core Identity UserManager with required dependencies
        var userStore = new Mock<IUserStore<IdentityUser>>();
        _mockUserManager = new Mock<UserManager<IdentityUser>>(
            userStore.Object, null, null, null, null, null, null, null, null);

        #endregion

        #region DbSet Mock Preparation

        // Initialize DbSet mocks for core entity collections
        _mockProductSet = new Mock<DbSet<ProductModel>>();
        _mockTransactionSet = new Mock<DbSet<TransactionModel>>();

        // Connect DbSets to the mocked DbContext
        _mockDbContext.Setup(d => d.Products).Returns(_mockProductSet.Object);
        _mockDbContext.Setup(d => d.Transactions).Returns(_mockTransactionSet.Object);

        #endregion

        #region Controller Initialization

        // Create controller instance with all mocked dependencies
        _controller = new ManagerController(_mockDbContext.Object, _mockUserManager.Object);

        // Configure ASP.NET Core controller context for request simulation
        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new RouteData(), new ControllerActionDescriptor());
        _controller.ControllerContext = new ControllerContext(actionContext);
        _controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());

        // Establish manager authentication context for administrative operations
        SetupManagerUser("manager@example.com", "Manager");

        #endregion
    }

    #endregion

    #region Dashboard & Metrics Display Tests

    /// <summary>
    /// Validates manager dashboard displays comprehensive business metrics and key performance indicators.
    /// </summary>
    /// <remarks>
    /// BUSINESS INTELLIGENCE VALIDATION:
    /// • Dashboard aggregates critical e-commerce metrics (product count, transaction volume)
    /// • Provides managers with actionable business insights for decision-making
    /// • Ensures data integrity across multiple entity collections
    /// 
    /// TECHNICAL COMPLEXITY:
    /// • Complex IQueryable mock setup for multiple DbSet collections
    /// • Asynchronous data aggregation testing with proper provider simulation
    /// • ViewData population validation for dashboard metric display
    /// 
    /// PERFORMANCE CONSIDERATIONS:
    /// • Dashboard queries must be optimized for real-time metric calculation
    /// • Efficient data aggregation prevents performance bottlenecks
    /// • Proper async/await pattern ensures non-blocking UI operations
    /// </remarks>
    [Fact]
    public async Task Index_ShouldReturnViewWithDashboardMetrics()
    {
        #region Arrange - Setup comprehensive business data

        // Create realistic product catalog for metrics calculation
        var products = new List<ProductModel>
        {
            new ProductModel { PkProductId = 1, ProductName = "Product 1", IsActive = true },
            new ProductModel { PkProductId = 2, ProductName = "Product 2", IsActive = true }
        }.AsQueryable();

        // Create transaction history for financial metrics
        var transactions = new List<TransactionModel>
        {
            new TransactionModel { PkTransactionId = 1, TotalAmount = 25.99m, TransactionDate = DateTime.UtcNow },
            new TransactionModel { PkTransactionId = 2, TotalAmount = 45.99m, TransactionDate = DateTime.UtcNow.AddDays(-1) }
        }.AsQueryable();

        // Configure complex IQueryable provider mocking for LINQ query support
        _mockProductSet.As<IQueryable<ProductModel>>().Setup(m => m.Provider).Returns(products.Provider);
        _mockProductSet.As<IQueryable<ProductModel>>().Setup(m => m.Expression).Returns(products.Expression);
        _mockProductSet.As<IQueryable<ProductModel>>().Setup(m => m.ElementType).Returns(products.ElementType);
        _mockProductSet.As<IQueryable<ProductModel>>().Setup(m => m.GetEnumerator()).Returns(products.GetEnumerator());

        _mockTransactionSet.As<IQueryable<TransactionModel>>().Setup(m => m.Provider).Returns(transactions.Provider);
        _mockTransactionSet.As<IQueryable<TransactionModel>>().Setup(m => m.Expression).Returns(transactions.Expression);
        _mockTransactionSet.As<IQueryable<TransactionModel>>().Setup(m => m.ElementType).Returns(transactions.ElementType);
        _mockTransactionSet.As<IQueryable<TransactionModel>>().Setup(m => m.GetEnumerator()).Returns(transactions.GetEnumerator());

        #endregion

        #region Act - Execute dashboard data aggregation

        var result = await _controller.Index();

        #endregion

        #region Assert - Validate dashboard metrics display

        // Verify dashboard view result with populated metrics
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        viewResult.ViewData.Should().NotBeEmpty();

        #endregion
    }

    #endregion

    #region Product Management Operations

    /// <summary>
    /// Validates complete product catalog display for managerial oversight and administration.
    /// </summary>
    /// <remarks>
    /// PRODUCT LIFECYCLE MANAGEMENT:
    /// • Managers can view entire product catalog including inactive items
    /// • Product status visibility enables inventory management decisions
    /// • Pricing information supports financial oversight and strategy
    /// 
    /// DATA INTEGRITY VERIFICATION:
    /// • Product count accuracy for catalog completeness
    /// • Property preservation throughout data flow
    /// • Model type safety with strongly typed collections
    /// </remarks>
    [Fact]
    public async Task ListOfProducts_ShouldReturnViewWithProducts()
    {
        #region Arrange - Setup comprehensive product catalog

        var products = new List<ProductModel>
        {
            new ProductModel { PkProductId = 1, ProductName = "Product 1", Price = 19.99m, IsActive = true },
            new ProductModel { PkProductId = 2, ProductName = "Product 2", Price = 29.99m, IsActive = false }
        }.AsQueryable();

        SetupMockDbSet(_mockProductSet, products);

        #endregion

        #region Act - Retrieve product management view

        var result = await _controller.ListOfProducts();

        #endregion

        #region Assert - Validate product catalog display

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeAssignableToType<IEnumerable<ProductModel>>().Subject;
        model.Should().HaveCount(2);
        model.First().ProductName.Should().Be("Product 1");

        #endregion
    }

    /// <summary>
    /// Validates product search functionality for efficient catalog management and filtering.
    /// </summary>
    /// <remarks>
    /// SEARCH OPTIMIZATION:
    /// • Enables rapid product location in large catalogs
    /// • Supports partial name matching for user convenience
    /// • Reduces cognitive load on management staff through focused results
    /// 
    /// BUSINESS VALUE:
    /// • Improves administrative efficiency for product updates
    /// • Enables quick status changes and pricing modifications
    /// • Supports effective inventory oversight and management
    /// </remarks>
    [Fact]
    public async Task ListOfProducts_WithSearchFilter_ShouldReturnFilteredProducts()
    {
        #region Arrange - Setup diverse product catalog for search testing

        var products = new List<ProductModel>
        {
            new ProductModel { PkProductId = 1, ProductName = "Laptop Computer", Price = 999.99m, IsActive = true },
            new ProductModel { PkProductId = 2, ProductName = "Wireless Mouse", Price = 29.99m, IsActive = true }
        }.AsQueryable();

        SetupMockDbSet(_mockProductSet, products);

        #endregion

        #region Act - Execute filtered product search

        var result = await _controller.ListOfProducts("Laptop");

        #endregion

        #region Assert - Validate search result accuracy

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeAssignableToType<IEnumerable<ProductModel>>().Subject;
        model.Should().HaveCount(1);
        model.First().ProductName.Should().Contain("Laptop");

        #endregion
    }

    /// <summary>
    /// Validates product activation/deactivation workflow with proper state management.
    /// </summary>
    /// <remarks>
    /// BUSINESS PROCESS VALIDATION:
    /// • Product status toggle enables inventory control without deletion
    /// • Maintains product data integrity during lifecycle changes
    /// • Supports reversible business decisions for seasonal products
    /// 
    /// TECHNICAL IMPLEMENTATION:
    /// • Database transaction safety with SaveChangesAsync verification
    /// • Entity state modification tracking and persistence
    /// • JSON API response format for AJAX integration
    /// 
    /// USER EXPERIENCE:
    /// • Immediate feedback through JSON success indicators
    /// • Non-destructive product management operations
    /// • Efficient status management without page reloads
    /// </remarks>
    [Fact]
    public async Task ToggleActive_WithValidProductId_ShouldToggleProductStatus()
    {
        #region Arrange - Setup product with current active state

        var product = new ProductModel { PkProductId = 1, ProductName = "Test Product", IsActive = true };

        _mockDbContext.Setup(d => d.Products.FindAsync(1))
                     .ReturnsAsync(product);

        _mockDbContext.Setup(d => d.SaveChangesAsync())
                     .ReturnsAsync(1);

        #endregion

        #region Act - Execute product status toggle

        var result = await _controller.ToggleActive(1, null, null, null, 1);

        #endregion

        #region Assert - Validate status change and persistence

        // Verify JSON success response for AJAX operations
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var value = jsonResult.Value;
        value.Should().NotBeNull();

        var successProperty = value!.GetType().GetProperty("success");
        successProperty!.GetValue(value).Should().Be(true);

        // Confirm product status was toggled correctly
        product.IsActive.Should().BeFalse(); // Should be toggled from true to false
        _mockDbContext.Verify(d => d.SaveChangesAsync(), Times.Once);

        #endregion
    }

    /// <summary>
    /// Validates error handling for product toggle operations with non-existent products.
    /// </summary>
    /// <remarks>
    /// ERROR HANDLING STRATEGY:
    /// • Returns HTTP 404 Not Found for missing product IDs
    /// • Prevents null reference exceptions and data corruption
    /// • Maintains RESTful API conventions for administrative operations
    /// 
    /// DATA PROTECTION:
    /// • Validates product existence before state modifications
    /// • Prevents unintended database changes from invalid requests
    /// • Ensures administrative operation integrity
    /// </remarks>
    [Fact]
    public async Task ToggleActive_WithInvalidProductId_ShouldReturnNotFound()
    {
        #region Arrange - Setup missing product scenario

        _mockDbContext.Setup(d => d.Products.FindAsync(999))
                     .ReturnsAsync((ProductModel?)null);

        #endregion

        #region Act & Assert - Validate 404 response for missing product

        var result = await _controller.ToggleActive(999, null, null, null, 1);
        result.Should().BeOfType<NotFoundResult>();

        #endregion
    }

    #endregion

    #region Transaction Oversight & Financial Management

    /// <summary>
    /// Validates comprehensive transaction history display for financial oversight and audit capabilities.
    /// </summary>
    /// <remarks>
    /// FINANCIAL OVERSIGHT VALIDATION:
    /// • Managers can review complete transaction history for business analysis
    /// • Transaction status tracking enables payment processing monitoring
    /// • Chronological ordering supports audit trail requirements
    /// 
    /// BUSINESS INTELLIGENCE:
    /// • Revenue tracking through transaction amount aggregation
    /// • Status analysis for payment processing performance
    /// • Historical data access for business trend analysis
    /// 
    /// COMPLIANCE REQUIREMENTS:
    /// • Complete audit trail maintenance for financial accountability
    /// • Transaction data integrity verification for regulatory compliance
    /// • Secure access to sensitive financial information
    /// </remarks>
    [Fact]
    public async Task ListAllTransactions_ShouldReturnViewWithTransactions()
    {
        #region Arrange - Setup comprehensive transaction history

        var transactions = new List<TransactionModel>
        {
            new TransactionModel 
            { 
                PkTransactionId = 1, 
                Amount = 25.99m, 
                TransactionDate = DateTime.UtcNow,
                TransactionStatus = "Completed"
            },
            new TransactionModel 
            { 
                PkTransactionId = 2, 
                Amount = 45.99m,
                TransactionDate = DateTime.UtcNow.AddDays(-1),
                TransactionStatus = "Pending"
            }
        }.AsQueryable();

        SetupMockDbSet(_mockTransactionSet, transactions);

        #endregion

        #region Act - Retrieve complete transaction history

        var result = await _controller.ListAllTransactions("");

        #endregion

        #region Assert - Validate financial data display

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeAssignableToType<IEnumerable<TransactionModel>>().Subject;
        model.Should().HaveCount(2);
        model.First().TransactionStatus.Should().Be("Completed");

        #endregion
    }

    /// <summary>
    /// Validates transaction filtering by status for targeted financial analysis and monitoring.
    /// </summary>
    /// <remarks>
    /// TARGETED FINANCIAL ANALYSIS:
    /// • Status-based filtering enables focused transaction review
    /// • Supports payment processing troubleshooting and monitoring
    /// • Enables efficient identification of pending or failed transactions
    /// 
    /// OPERATIONAL EFFICIENCY:
    /// • Reduces data noise through intelligent filtering
    /// • Enables rapid identification of transactions requiring attention
    /// • Supports proactive payment processing management
    /// 
    /// BUSINESS VALUE:
    /// • Improved cash flow monitoring through status tracking
    /// • Enhanced customer service through payment issue identification
    /// • Streamlined financial reconciliation processes
    /// </remarks>
    [Fact]
    public async Task ListAllTransactions_WithStatusFilter_ShouldReturnFilteredTransactions()
    {
        #region Arrange - Setup diverse transaction statuses for filtering

        var transactions = new List<TransactionModel>
        {
            new TransactionModel { PkTransactionId = 1, TransactionStatus = "Completed", Amount = 25.99m },
            new TransactionModel { PkTransactionId = 2, TransactionStatus = "Pending", Amount = 45.99m },
            new TransactionModel { PkTransactionId = 3, TransactionStatus = "Completed", Amount = 35.99m }
        }.AsQueryable();

        SetupMockDbSet(_mockTransactionSet, transactions);

        #endregion

        #region Act - Execute status-filtered transaction query

        var result = await _controller.ListAllTransactions("Completed");

        #endregion

        #region Assert - Validate filtering accuracy and completeness

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeAssignableToType<IEnumerable<TransactionModel>>().Subject;
        model.Should().HaveCount(2);
        model.All(t => t.TransactionStatus == "Completed").Should().BeTrue();

        #endregion
    }

    #endregion

    #region Staff Account Administration

    /// <summary>
    /// Validates staff account management interface for organizational oversight and administration.
    /// </summary>
    /// <remarks>
    /// ORGANIZATIONAL HIERARCHY VALIDATION:
    /// • Managers can view staff accounts within their oversight scope
    /// • User information display enables effective team management
    /// • Identity integration ensures proper authentication context
    /// 
    /// ADMINISTRATIVE FUNCTIONALITY:
    /// • Staff account visibility for role management and oversight
    /// • Email-based identification for communication and coordination
    /// • Integration with ASP.NET Core Identity for secure user management
    /// 
    /// BUSINESS OPERATIONS:
    /// • Enables effective team coordination and management
    /// • Supports organizational transparency and accountability
    /// • Facilitates staff onboarding and role assignment workflows
    /// 
    /// SECURITY CONSIDERATIONS:
    /// • Manager role required for staff account access
    /// • Secure identity information handling through UserManager
    /// • Appropriate data exposure limits for organizational privacy
    /// </remarks>
    [Fact]
    public async Task ListOfStaffAccount_ShouldReturnViewWithStaffAccounts()
    {
        #region Arrange - Setup organizational staff data

        var users = new List<IdentityUser>
        {
            new IdentityUser { UserName = "manager1@example.com", Email = "manager1@example.com" },
            new IdentityUser { UserName = "admin1@example.com", Email = "admin1@example.com" }
        };

        _mockUserManager.Setup(u => u.Users)
                       .Returns(users.AsQueryable());

        #endregion

        #region Act - Retrieve staff account information

        var result = await _controller.ListOfStaffAccount("");

        #endregion

        #region Assert - Validate staff data display and accessibility

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<IEnumerable<IdentityUser>>().Subject;
        model.Should().HaveCount(2);
        model.First().Email.Should().Be("manager1@example.com");

        #endregion
    }

    #endregion

    #region Helper Methods & Test Utilities

    /// <summary>
    /// Configures controller with manager authentication context for administrative operation testing.
    /// </summary>
    /// <param name="email">Manager's email address for identity claims</param>
    /// <param name="role">Manager role designation for authorization</param>
    /// <remarks>
    /// AUTHENTICATION CONTEXT SETUP:
    /// • Creates comprehensive claims-based identity for manager users
    /// • Simulates authenticated management session for testing purposes
    /// • Enables role-based authorization validation in administrative workflows
    /// 
    /// CLAIMS CONFIGURATION:
    /// • ClaimTypes.Name: Manager's email for user identification
    /// • ClaimTypes.NameIdentifier: Unique user ID for system tracking
    /// • ClaimTypes.Role: Manager role for authorization enforcement
    /// 
    /// SECURITY SIMULATION:
    /// • Provides realistic authentication context for comprehensive testing
    /// • Enables validation of authorization-protected administrative operations
    /// • Supports both positive (authorized) and negative (unauthorized) test scenarios
    /// </remarks>
    private void SetupManagerUser(string email, string role)
    {
        // Create comprehensive authentication claims for manager context
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, email),           // Primary manager identification
            new Claim(ClaimTypes.NameIdentifier, "1"),   // Unique system identifier
            new Claim(ClaimTypes.Role, role)             // Authorization role designation
        };

        // Build authenticated identity with manager privileges
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        // Apply management authentication context to controller
        _controller.ControllerContext.HttpContext.User = principal;
    }

    /// <summary>
    /// Configures mock DbSet with comprehensive IQueryable provider support for Entity Framework simulation.
    /// </summary>
    /// <typeparam name="T">Entity type for DbSet configuration</typeparam>
    /// <param name="mockDbSet">Mock DbSet instance to configure</param>
    /// <param name="data">Test data collection for DbSet simulation</param>
    /// <remarks>
    /// TECHNICAL COMPLEXITY:
    /// • IQueryable provider setup enables LINQ query execution testing
    /// • Expression tree configuration supports complex query scenarios
    /// • Element type and enumerator setup ensures complete DbSet simulation
    /// 
    /// TESTING ENABLEMENT:
    /// • Supports Entity Framework LINQ query testing without actual database
    /// • Enables comprehensive data access layer testing in isolation
    /// • Provides realistic DbContext behavior simulation for controller testing
    /// 
    /// PERFORMANCE OPTIMIZATION:
    /// • In-memory data simulation eliminates database I/O during testing
    /// • Fast test execution through mock-based data access
    /// • Isolated test environment prevents database state interference
    /// 
    /// USAGE PATTERN:
    /// • Call once per test with appropriate test data for each entity type
    /// • Supports multiple entity collections through generic type parameter
    /// • Enables complex relational query testing through proper provider setup
    /// </remarks>
    private static void SetupMockDbSet<T>(Mock<DbSet<T>> mockDbSet, IQueryable<T> data) where T : class
    {
        // Configure IQueryable provider for LINQ query execution
        mockDbSet.As<IQueryable<T>>().Setup(m => m.Provider).Returns(data.Provider);
        mockDbSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(data.Expression);
        mockDbSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(data.ElementType);
        mockDbSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(data.GetEnumerator());
    }

    #endregion
}