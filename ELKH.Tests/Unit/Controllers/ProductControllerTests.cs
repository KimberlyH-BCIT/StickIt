using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ELKH.Data;
using System.Reflection;
using System.Text.Json;
using ELKH.Services;

namespace ELKH.Tests.Unit.Controllers;

/// <summary>
/// Comprehensive unit tests for ProductController functionality in the StickIt e-commerce application.
/// 
/// This test suite validates product display, search operations, pricing queries, and administrative features.
/// Tests ensure proper data flow, authorization enforcement, and API contract compliance.
/// </summary>
/// <remarks>
/// TABLE OF CONTENTS (~320 lines)
/// ================================================================================
/// 1. Test Setup & Configuration ..................................... Lines   60-90
///    - Constructor & Dependencies                   // Mock service setup and controller initialization
///    - SetupUserRole()                             // Helper for role-based authorization testing
/// 
/// 2. Product Display & Retrieval Tests .............................. Lines   92-180
///    - Index_ShouldReturnViewWithProducts()        // Product catalog display validation
///    - Details_WithValidId_ShouldReturnViewWithProduct()        // Individual product view
///    - Details_WithInvalidId_ShouldReturnNotFound()            // Error handling for missing products
/// 
/// 3. Product Search & Query Operations ............................... Lines  182-260
///    - SearchNames_ShouldReturnJsonWithResults()              // Product name search functionality
///    - SearchNames_WithEmptyQuery_ShouldReturnEmptyResults()  // Search input validation
///    - GetPrice_WithValidId_ShouldReturnJsonWithPrice()       // Price query API validation
/// 
/// 4. Administrative Operations (Authorization Required) .............. Lines  262-320
///    - Create_WithAdminOrManagerRole_ShouldReturnView()       // Role-based access control
///    - Create_Post_WithValidModel_ShouldCreateProduct()       // Product creation workflow
///    - Create_WithoutAuthorization_ShouldDenyAccess()         // Unauthorized access testing
///    - Create_Post_WithInvalidModel_ShouldReturnView()        // Model validation testing
///    - Create_Post_WithServiceError_ShouldHandleGracefully()  // Error handling testing
/// ================================================================================
/// 
/// TESTING PATTERNS:
/// • Service layer mocking for isolated controller testing
/// • Role-based authorization testing with claims identity simulation
/// • JSON API response validation with reflection-based property checking
/// • Theory-based testing for multiple authorization roles
/// • Comprehensive error scenario coverage (404, validation failures)
/// 
/// BUSINESS RULES VALIDATED:
/// • Product catalog displays all available products
/// • Product details include ratings and comprehensive information
/// • Search functionality returns relevant results with scoring
/// • Administrative operations require proper role authorization (Admin/Manager)
/// • Empty search queries are optimized to avoid unnecessary service calls
/// • Price queries support real-time pricing for dynamic user interfaces
/// 
/// SECURITY CONSIDERATIONS:
/// • Role-based access control for administrative functions
/// • Input validation for search queries and product IDs
/// • Secure handling of user claims and authorization context
/// • Protection against unauthorized product creation/modification
/// </remarks>
public class ProductControllerTests
{
    #region Test Setup & Configuration

    // ── Mock Dependencies ──
    private readonly Mock<ISearchService> _mockSearchService;
    private readonly Mock<IProductService> _mockProductService;
    private readonly Mock<IRatingService> _mockRatingService;
    private readonly Mock<IUserService> _mockUserService;
    private readonly Mock<IStockNotificationService> _mockStockNotificationService;
    private readonly ProductController _controller;

    /// <summary>
    /// Initializes test environment with comprehensive mocked dependencies and controller context.
    /// </summary>
    /// <remarks>
    /// DEPENDENCY INJECTION SETUP:
    /// • ISearchService: Fuzzy product name searching and autocomplete
    /// • IProductService: Core product CRUD and search operations
    /// • IRatingService: Product rating aggregation and display
    /// • IUserService: User context and authorization support
    /// 
    /// CONTROLLER CONFIGURATION:
    /// • ASP.NET Core context simulation for request processing
    /// • TempData provider for cross-request messaging
    /// • Route data and action descriptor for proper MVC simulation
    /// 
    /// This setup enables comprehensive testing of controller behavior
    /// while maintaining isolation from actual service implementations.
    /// </remarks>
    public ProductControllerTests()
    {
        // Initialize mock services with flexible verification capabilities
        _mockSearchService = new Mock<ISearchService>();
        _mockProductService = new Mock<IProductService>();
        _mockRatingService = new Mock<IRatingService>();
        _mockUserService = new Mock<IUserService>();
        _mockStockNotificationService = new Mock<IStockNotificationService>();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"ProductControllerTests_{Guid.NewGuid()}")
            .Options;
        var context = new ApplicationDbContext(options);

        // Create controller instance with all required dependencies
        _controller = new ProductController(
            context,
            _mockSearchService.Object,
            _mockProductService.Object,
            _mockRatingService.Object,
            _mockUserService.Object,
            _mockStockNotificationService.Object);

        // Configure ASP.NET Core request context for controller testing
        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new RouteData(), new ControllerActionDescriptor());
        _controller.ControllerContext = new ControllerContext(actionContext);
        _controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
    }

    #endregion

    #region Product Display & Retrieval Tests

    /// <summary>
    /// Validates the product catalog index page displays all available products correctly.
    /// </summary>
    /// <remarks>
    /// BUSINESS FUNCTIONALITY:
    /// • Product service retrieves complete product catalog
    /// • Controller passes data to view without modification
    /// • Products maintain essential e-commerce properties (ID, name, price)
    /// 
    /// TECHNICAL VALIDATION:
    /// • View result type verification
    /// • Model type safety with strongly typed List&lt;ProductVM&gt;
    /// • Product count and property integrity checks
    /// </remarks>
    [Fact]
    public async Task Index_ShouldReturnViewWithProducts()
    {
        #region Arrange - Setup product catalog data

        var products = CreateTestProductList();
        _mockProductService.Setup(p => p.GetPagedCatalogAsync(null, null, "name_asc", 0, 12, It.IsAny<CancellationToken>()))
                          .ReturnsAsync(new PagedResult<ProductVM>
                          {
                              Items = products,
                              TotalCount = 25
                          });
        _mockProductService.Setup(p => p.GetCategoriesAsync(It.IsAny<CancellationToken>()))
                          .ReturnsAsync(new List<CategoryModel>());

        #endregion

        #region Act - Execute catalog retrieval

        var result = await _controller.Index(null, null, "name_asc");

        #endregion

        #region Assert - Validate catalog display

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<List<ProductVM>>().Subject;
        model.Should().HaveCount(2);
        model.First().ProductName.Should().Be("Product 1");
        ((int)_controller.ViewBag.Total).Should().Be(25);
        ((bool)_controller.ViewBag.HasMore).Should().BeTrue();

        _mockProductService.Verify(p => p.GetPagedCatalogAsync(null, null, "name_asc", 0, 12, It.IsAny<CancellationToken>()), Times.Once);
        _mockProductService.Verify(p => p.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);

        #endregion
    }

    [Fact]
    public async Task LoadMore_ShouldRequestNextCatalogPageOnly()
    {
        var nextPage = new List<ProductVM>
        {
            CreateTestProduct(13, "Product 13", 20.99m),
            CreateTestProduct(14, "Product 14", 21.99m)
        };

        _mockProductService.Setup(p => p.GetPagedCatalogAsync("sticker", 4, "price_desc", 12, 12, It.IsAny<CancellationToken>()))
                          .ReturnsAsync(new PagedResult<ProductVM>
                          {
                              Items = nextPage,
                              TotalCount = 14
                          });

        var result = await _controller.LoadMore("sticker", 4, "price_desc", 12);

        var partial = result.Should().BeOfType<PartialViewResult>().Subject;
        partial.ViewName.Should().Be("_ProductCardBatch");
        partial.Model.Should().BeEquivalentTo(nextPage);

        _mockProductService.Verify(p => p.GetPagedCatalogAsync("sticker", 4, "price_desc", 12, 12, It.IsAny<CancellationToken>()), Times.Once);
        _mockProductService.Verify(p => p.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Validates product detail view with comprehensive product information and ratings.
    /// </summary>
    /// <remarks>
    /// INTEGRATION COMPLEXITY:
    /// • Combines product data from ProductService
    /// • Aggregates rating information from RatingService
    /// • Ensures complete product presentation for user decision-making
    /// 
    /// BUSINESS VALUE:
    /// • Detailed product information supports purchasing decisions
    /// • Rating integration provides social proof and quality indicators
    /// • Proper error handling for data consistency
    /// </remarks>
    [Fact]
    public async Task Details_WithValidId_ShouldReturnViewWithProduct()
    {
        #region Arrange - Setup detailed product and rating data

        var product = CreateTestProduct();
        _mockProductService.Setup(p => p.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                          .ReturnsAsync(product);

        // Setup rating service to provide social proof data
        _mockRatingService.Setup(r => r.GetPagedApprovedReviewsAsync(1, 1, "date_new", It.IsAny<CancellationToken>()))
                         .ReturnsAsync(new ReviewPageVM());

        #endregion

        #region Act - Retrieve product details

        var result = await _controller.Details(1);

        #endregion

        #region Assert - Validate detailed product display

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<ProductVM>().Subject;
        model.ProductId.Should().Be(1);
        model.ProductName.Should().Be("Test Product");

        #endregion
    }

    /// <summary>
    /// Validates proper error handling for non-existent product requests.
    /// </summary>
    /// <remarks>
    /// ERROR HANDLING STRATEGY:
    /// • Returns HTTP 404 Not Found for missing products
    /// • Prevents null reference exceptions in views
    /// • Maintains RESTful API conventions for resource access
    /// 
    /// USER EXPERIENCE:
    /// • Clear error indication for invalid product links
    /// • Graceful degradation from broken/outdated URLs
    /// • SEO-friendly 404 responses for search engines
    /// </remarks>
    [Fact]
    public async Task Details_WithInvalidId_ShouldRedirectWithWarning()
    {
        #region Arrange - Setup missing product scenario

        _mockProductService.Setup(p => p.GetByIdAsync(999, It.IsAny<CancellationToken>()))
                          .ReturnsAsync((ProductVM?)null);

        #endregion

        #region Act & Assert - Validate redirect response

        var result = await _controller.Details(999);
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(ProductController.Index));
        _controller.TempData["Message"].Should().Be("warning, Unable to find product ID: 999");

        #endregion
    }

    #endregion

    #region Product Search & Query Operations

    /// <summary>
    /// Validates product search functionality with scored results for autocomplete features.
    /// </summary>
    /// <remarks>
    /// SEARCH ALGORITHM VALIDATION:
    /// • Service layer performs fuzzy matching and relevance scoring
    /// • Results include relevance scores for ranking optimization
    /// • JSON response format supports AJAX autocomplete integration
    /// 
    /// USER EXPERIENCE ENHANCEMENT:
    /// • Real-time search suggestions improve product discovery
    /// • Scored results enable intelligent result ranking
    /// • Fast API response times support responsive user interfaces
    /// </remarks>
    [Fact]
    public async Task SearchNames_ShouldReturnJsonWithResults()
    {
        #region Arrange - Setup search results with scoring

        var searchResults = CreateTestSearchResults();
        _mockSearchService.Setup(s => s.SearchNames("test"))
                          .ReturnsAsync(searchResults);

        #endregion

        #region Act - Execute product name search

        var result = await _controller.SearchNames("test");

        #endregion

        #region Assert - Validate JSON response structure

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var value = jsonResult.Value;
        value.Should().NotBeNull();

        // The controller returns an anonymous object array, not SearchResultDto directly
        var resultList = JsonSerializer.Deserialize<List<object>>(JsonSerializer.Serialize(value));
        resultList.Should().HaveCount(1);

        #endregion
    }

    /// <summary>
    /// Validates search optimization by avoiding unnecessary service calls for empty queries.
    /// </summary>
    /// <remarks>
    /// PERFORMANCE OPTIMIZATION:
    /// • Empty search queries return immediately without service layer calls
    /// • Reduces database load from incomplete user input
    /// • Improves overall application responsiveness
    /// 
    /// BUSINESS LOGIC:
    /// • Empty results guide user to provide more specific search terms
    /// • Prevents overwhelming users with complete product catalogs
    /// • Encourages intentional product discovery behavior
    /// </remarks>
    [Fact]
    public async Task SearchNames_WithEmptyQuery_ShouldReturnEmptyResults()
    {
        #region Act & Assert - Validate empty query optimization

        var result = await _controller.SearchNames("");

        // Verify immediate empty result without service calls
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var model = jsonResult.Value.Should().BeAssignableTo<IEnumerable<object>>().Subject;
        model.Should().BeEmpty();

        // Confirm search service was never invoked (performance optimization)
        _mockSearchService.Verify(s => s.SearchNames(It.IsAny<string>()), Times.Never);

        #endregion
    }

    /// <summary>
    /// Validates real-time price query API for dynamic pricing displays and cart calculations.
    /// </summary>
    /// <remarks>
    /// REAL-TIME PRICING SUPPORT:
    /// • Enables dynamic price updates without full page reloads
    /// • Supports cart calculation updates and promotional pricing
    /// • JSON API format compatible with modern frontend frameworks
    /// 
    /// TECHNICAL IMPLEMENTATION:
    /// • Uses reflection to validate anonymous object structure
    /// • Ensures decimal precision for accurate financial calculations
    /// • Provides consistent API response format for JavaScript consumption
    /// </remarks>
    [Fact]
    public async Task GetPrice_WithValidId_ShouldReturnJsonWithPrice()
    {
        #region Arrange - Setup product with pricing data

        var product = CreateTestProduct(1, "Test Product", 25.99m);
        _mockProductService.Setup(p => p.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                          .ReturnsAsync(product);

        #endregion

        #region Act - Query product price

        var result = await _controller.GetPrice(1);

        #endregion

        #region Assert - Validate JSON price response

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var value = jsonResult.Value;
        value.Should().NotBeNull();

        // Verify price property in anonymous object using reflection
        var priceProperty = value!.GetType().GetProperty("price");
        priceProperty.Should().NotBeNull();
        priceProperty!.GetValue(value).Should().Be(25.99m);

        #endregion
    }

    #endregion

    #region Administrative Operations (Authorization Required)

    /// <summary>
    /// Validates that the create GET action returns a view with an empty ProductVM and category options.
    /// </summary>
    [Fact]
    public async Task Create_Get_ShouldReturnViewWithEmptyModel()
    {
        #region Arrange - Setup category options

        var categories = new List<CategoryModel>
        {
            new CategoryModel { PkCategoryId = 1, CategoryName = "Category 1" },
            new CategoryModel { PkCategoryId = 2, CategoryName = "Category 2" }
        };

        _mockProductService.Setup(p => p.GetCategoriesAsync())
                          .ReturnsAsync(categories);

        #endregion

        #region Act - Execute create GET

        var result = await _controller.Create();

        #endregion

        #region Assert - Validate view and model

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<ProductVM>().Subject;

        // Should be empty model for new product creation
        model.ProductId.Should().Be(0);
        model.ProductName.Should().BeEmpty();

        // ViewBag should contain category options
        var categoryOptions = ((object)_controller.ViewBag.CategoryId).Should().BeAssignableTo<IEnumerable<SelectListItem>>().Subject;
        categoryOptions.Should().HaveCount(2);

        #endregion
    }

    /// <summary>
    /// Validates successful product creation with valid model and proper redirect.
    /// </summary>
    [Fact]
    public async Task Create_Post_WithValidModel_ShouldCreateProduct()
    {
        #region Arrange - Setup valid product and categories

        var productVM = CreateTestProduct(0, "New Product", 29.99m);
        productVM.CategoryId = 1;

        var categories = new List<CategoryModel>
        {
            new CategoryModel { PkCategoryId = 1, CategoryName = "Category 1" }
        };

        _mockProductService.Setup(p => p.GetCategoriesAsync())
                          .ReturnsAsync(categories);

        _mockProductService.Setup(p => p.CreateAsync(It.IsAny<ProductVM>(), It.IsAny<CancellationToken>()))
                          .Returns(Task.CompletedTask);

        #endregion

        #region Act - Execute product creation

        var result = await _controller.Create(productVM);

        #endregion

        #region Assert - Validate creation workflow

        // Verify successful redirect to product index
        var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirectResult.ActionName.Should().Be("Index");

        // Confirm service received correct product data
        _mockProductService.Verify(p => p.CreateAsync(It.Is<ProductVM>(pm =>
            pm.ProductName == "New Product" && pm.Price == 29.99m), It.IsAny<CancellationToken>()), Times.Once);

        // Verify TempData success message
        _controller.TempData["Message"].Should().Be("success, Product created successfully");

        #endregion
    }

    /// <summary>
    /// Validates product creation form re-display with model validation errors.
    /// </summary>
    [Fact]
    public async Task Create_Post_WithInvalidModel_ShouldReturnView()
    {
        #region Arrange - Setup invalid product data

        var productVM = new ProductVM
        {
            // Missing required ProductName
            Price = -10.00m, // Invalid negative price
            CategoryId = 0    // Invalid category
        };

        var categories = new List<CategoryModel>
        {
            new CategoryModel { PkCategoryId = 1, CategoryName = "Category 1" }
        };

        _mockProductService.Setup(p => p.GetCategoriesAsync())
                          .ReturnsAsync(categories);

        // Simulate model state error
        _controller.ModelState.AddModelError("ProductName", "Product name is required");

        #endregion

        #region Act - Execute product creation with invalid model

        var result = await _controller.Create(productVM);

        #endregion

        #region Assert - Validate validation failure handling

        // Should return view with model errors, not redirect
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        viewResult.Model.Should().BeOfType<ProductVM>();

        // Service should not be called with invalid model
        _mockProductService.Verify(p => p.CreateAsync(It.IsAny<ProductVM>(), It.IsAny<CancellationToken>()),
                                  Times.Never);

        // Should add helpful error message
        _controller.ModelState.Should().ContainKey("");

        #endregion
    }

    /// <summary>
    /// Validates the edit GET action returns a view with populated ProductVM and category options.
    /// </summary>
    [Fact]
    public async Task Edit_Get_WithValidId_ShouldReturnViewWithProduct()
    {
        #region Arrange - Setup existing product and categories

        var product = CreateTestProduct(1, "Existing Product", 19.99m);
        product.CategoryId = 1;

        var categories = new List<CategoryModel>
        {
            new CategoryModel { PkCategoryId = 1, CategoryName = "Category 1" },
            new CategoryModel { PkCategoryId = 2, CategoryName = "Category 2" }
        };

        _mockProductService.Setup(p => p.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                          .ReturnsAsync(product);

        _mockProductService.Setup(p => p.GetCategoriesAsync())
                          .ReturnsAsync(categories);

        #endregion

        #region Act - Execute edit GET

        var result = await _controller.Edit(1);

        #endregion

        #region Assert - Validate view and model

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<ProductVM>().Subject;

        model.ProductId.Should().Be(1);
        model.ProductName.Should().Be("Existing Product");
        model.Price.Should().Be(19.99m);

        // ViewBag should contain category options
        var categoryOptions = ((object)_controller.ViewBag.CategoryId).Should().BeAssignableTo<IEnumerable<SelectListItem>>().Subject;
        categoryOptions.Should().HaveCount(2);

        #endregion
    }

    /// <summary>
    /// Validates edit GET action redirects with warning for non-existent product.
    /// </summary>
    [Fact]
    public async Task Edit_Get_WithInvalidId_ShouldRedirectWithWarning()
    {
        #region Arrange - Setup missing product

        _mockProductService.Setup(p => p.GetByIdAsync(999, It.IsAny<CancellationToken>()))
                          .ReturnsAsync((ProductVM?)null);

        #endregion

        #region Act - Execute edit GET with invalid ID

        var result = await _controller.Edit(999);

        #endregion

        #region Assert - Validate redirect with warning

        var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirectResult.ActionName.Should().Be("Index");

        // Verify warning message in TempData
        _controller.TempData["Message"].Should().Be("warning, Unable to find product ID: 999");

        #endregion
    }

    /// <summary>
    /// Validates successful product update with valid model and proper redirect.
    /// </summary>
    [Fact]
    public async Task Edit_Post_WithValidModel_ShouldUpdateProduct()
    {
        #region Arrange - Setup valid product update

        var productVM = CreateTestProduct(1, "Updated Product", 24.99m);
        productVM.CategoryId = 1;

        var existingProduct = CreateTestProduct(1, "Original Product", 19.99m);

        var categories = new List<CategoryModel>
        {
            new CategoryModel { PkCategoryId = 1, CategoryName = "Category 1" }
        };

        _mockProductService.Setup(p => p.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                          .ReturnsAsync(existingProduct);

        _mockProductService.Setup(p => p.GetCategoriesAsync())
                          .ReturnsAsync(categories);

        _mockProductService.Setup(p => p.UpdateAsync(It.IsAny<ProductVM>(), It.IsAny<CancellationToken>()))
                          .Returns(Task.CompletedTask);

        #endregion

        #region Act - Execute product update

        var result = await _controller.Edit(productVM);

        #endregion

        #region Assert - Validate update workflow

        // Verify successful redirect to product index
        var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirectResult.ActionName.Should().Be("Index");

        // Confirm service received correct product data
        _mockProductService.Verify(p => p.UpdateAsync(It.Is<ProductVM>(pm =>
            pm.ProductName == "Updated Product" && pm.Price == 24.99m), It.IsAny<CancellationToken>()), Times.Once);

        // Verify TempData success message
        _controller.TempData["Message"].Should().Be("success, Product updated successfully");

        #endregion
    }

    /// <summary>
    /// Validates the delete GET action returns a confirmation view with product details.
    /// </summary>
    [Fact]
    public async Task Delete_Get_WithValidId_ShouldReturnConfirmationView()
    {
        #region Arrange - Setup existing product

        var product = CreateTestProduct(1, "Product to Delete", 15.99m);

        _mockProductService.Setup(p => p.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                          .ReturnsAsync(product);

        #endregion

        #region Act - Execute delete GET

        var result = await _controller.Delete(1);

        #endregion

        #region Assert - Validate confirmation view

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<ProductVM>().Subject;

        model.ProductId.Should().Be(1);
        model.ProductName.Should().Be("Product to Delete");

        #endregion
    }

    /// <summary>
    /// Validates successful product deletion with proper redirect and confirmation.
    /// </summary>
    [Fact]
    public async Task Delete_Post_WithValidId_ShouldDeleteProduct()
    {
        #region Arrange - Setup existing product

        var existingProduct = CreateTestProduct(1, "Product to Delete", 15.99m);

        _mockProductService.Setup(p => p.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                          .ReturnsAsync(existingProduct);

        _mockProductService.Setup(p => p.DeleteAsync(1, It.IsAny<CancellationToken>()))
                          .Returns(Task.CompletedTask);

        #endregion

        #region Act - Execute product deletion

        var result = await _controller.DeleteConfirmed(1);

        #endregion

        #region Assert - Validate deletion workflow

        // Verify successful redirect to product index
        var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirectResult.ActionName.Should().Be("Index");

        // Confirm service performed deletion
        _mockProductService.Verify(p => p.DeleteAsync(1, It.IsAny<CancellationToken>()), Times.Once);

        // Verify TempData success message
        _controller.TempData["Message"].Should().Be("success, Product archived successfully");

        #endregion
    }

    /// <summary>
    /// Validates role-based access control for administrative product operations.
    /// </summary>
    /// <remarks>
    /// Note: In a real application, role-based authorization would be enforced through
    /// ASP.NET Core Authorization middleware. This test validates the controller
    /// behavior assuming authorization passes. Integration tests should verify
    /// the actual authorization enforcement.
    /// </remarks>
    [Theory]
    [InlineData("Admin")]     // Administrative users can manage products
    [InlineData("Manager")]   // Management users can manage products
    public async Task AdminOperations_WithAuthorizedRole_ShouldHaveAccess(string role)
    {
        #region Arrange - Setup user role context

        SetupUserRole(role);

        var categories = new List<CategoryModel>
        {
            new CategoryModel { PkCategoryId = 1, CategoryName = "Category 1" }
        };

        _mockProductService.Setup(p => p.GetCategoriesAsync())
                          .ReturnsAsync(categories);

        #endregion

        #region Act & Assert - Validate authorized access to admin operations

        // Test Create GET access
        var createResult = await _controller.Create();
        createResult.Should().BeOfType<ViewResult>();

        // Test Edit GET access (assuming product exists)
        var product = CreateTestProduct();
        _mockProductService.Setup(p => p.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                          .ReturnsAsync(product);

        var editResult = await _controller.Edit(1);
        editResult.Should().BeOfType<ViewResult>();

        // Test Delete GET access
        var deleteResult = await _controller.Delete(1);
        deleteResult.Should().BeOfType<ViewResult>();

        #endregion
    }

    /// <summary>
    /// Verifies that all administrative product-management actions are explicitly protected
    /// by the Admin/Manager role requirement.
    /// </summary>
    [Fact]
    public void ProductManagementActions_ShouldRequireAdminOrManagerRole()
    {
        AssertAdminManagerAuthorization(GetAction(nameof(ProductController.Create), Type.EmptyTypes));
        AssertAdminManagerAuthorization(GetAction(nameof(ProductController.Create), typeof(ProductVM)));
        AssertAdminManagerAuthorization(GetAction(nameof(ProductController.Edit), typeof(int)));
        AssertAdminManagerAuthorization(GetAction(nameof(ProductController.Edit), typeof(ProductVM)));
        AssertAdminManagerAuthorization(GetAction(nameof(ProductController.Delete), typeof(int)));
        AssertAdminManagerAuthorization(GetAction(nameof(ProductController.DeleteConfirmed), typeof(int)));
    }

    [Fact]
    public void CustomerRole_ShouldNotSatisfyAdminProductManagementRoleRequirement()
    {
        var protectedActions = new[]
        {
            GetAction(nameof(ProductController.Create), Type.EmptyTypes),
            GetAction(nameof(ProductController.Create), typeof(ProductVM)),
            GetAction(nameof(ProductController.Edit), typeof(int)),
            GetAction(nameof(ProductController.Edit), typeof(ProductVM)),
            GetAction(nameof(ProductController.Delete), typeof(int)),
            GetAction(nameof(ProductController.DeleteConfirmed), typeof(int))
        };

        foreach (var action in protectedActions)
        {
            var authorizeAttribute = action.GetCustomAttribute<AuthorizeAttribute>();
            authorizeAttribute.Should().NotBeNull();
            authorizeAttribute!.Roles!.Split(',').Should().NotContain("Customer");
        }
    }

    #endregion

    #region Helper Methods & Utilities

    private static MethodInfo GetAction(string methodName, params Type[] parameterTypes)
    {
        var method = typeof(ProductController).GetMethod(methodName, parameterTypes);
        method.Should().NotBeNull($"Expected to find ProductController.{methodName}.");
        return method!;
    }

    private static void AssertAdminManagerAuthorization(MethodInfo method)
    {
        var authorizeAttribute = method.GetCustomAttribute<AuthorizeAttribute>();

        authorizeAttribute.Should().NotBeNull($"{method.Name} should require explicit role authorization.");
        authorizeAttribute!.Roles.Should().Be("Admin,Manager");
    }

    /// <summary>
    /// Creates a test product with default or specified values to reduce test data redundancy.
    /// </summary>
    /// <param name="id">Product ID</param>
    /// <param name="name">Product name</param>
    /// <param name="price">Product price</param>
    /// <param name="description">Product description</param>
    /// <returns>ProductVM with test data</returns>
    private static ProductVM CreateTestProduct(int id = 1, string name = "Test Product",
        decimal price = 19.99m, string description = "Test Description")
    {
        return new ProductVM
        {
            ProductId = id,
            ProductName = name,
            Price = price,
            Description = description
        };
    }

    /// <summary>
    /// Creates a list of test products to reduce setup redundancy.
    /// </summary>
    /// <returns>List of ProductVM for testing</returns>
    private static List<ProductVM> CreateTestProductList()
    {
        return new List<ProductVM>
        {
            CreateTestProduct(1, "Product 1", 10.99m),
            CreateTestProduct(2, "Product 2", 15.99m)
        };
    }

    /// <summary>
    /// Creates test search results to reduce setup redundancy.
    /// </summary>
    /// <returns>List of SearchResultDto for testing</returns>
    private static List<SearchResultDto> CreateTestSearchResults()
    {
        return new List<SearchResultDto>
        {
            new SearchResultDto { Id = 1, Name = "Matching Product", Price = 19.99m }
        };
    }

    /// <summary>
    /// Configures controller with specific user role for authorization testing.
    /// </summary>
    /// <param name="role">User role for claims-based authorization (Admin, Manager, etc.)</param>
    /// <remarks>
    /// AUTHORIZATION CONTEXT SETUP:
    /// • Creates claims-based identity with specified role
    /// • Simulates authenticated user with role-based permissions
    /// • Enables comprehensive authorization testing across different user types
    /// 
    /// CLAIMS CONFIGURATION:
    /// • ClaimTypes.Name: User's email address for identification
    /// • ClaimTypes.Role: User's role for authorization decisions
    /// • Identity type: "Test" for test environment distinction
    /// 
    /// USAGE PATTERN:
    /// • Call before executing controller actions requiring authorization
    /// • Supports testing both positive (authorized) and negative (unauthorized) scenarios
    /// • Enables role-specific feature validation
    /// </remarks>
    private void SetupUserRole(string role)
    {
        // Create authorization claims for test scenarios
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, "test@example.com"),  // User identity
            new Claim(ClaimTypes.Role, role)                 // Authorization role
        };

        // Build authenticated identity with role-based permissions
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        // Apply authorization context to controller
        _controller.ControllerContext.HttpContext.User = principal;
    }

    #endregion
}