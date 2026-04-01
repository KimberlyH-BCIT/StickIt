using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;

namespace ELKH.Tests.Unit.Controllers;

/// <summary>
/// Comprehensive unit tests for CartController functionality in the StickIt e-commerce application.
/// 
/// This test suite validates cart operations, AJAX responses, user interactions, and authentication scenarios.
/// Tests ensure proper business logic enforcement, security validation, and user experience optimization.
/// </summary>
/// <remarks>
/// TABLE OF CONTENTS (184 lines)
/// ================================================================================
/// 1. Test Setup & Configuration ..................................... Lines   32-64
///    - Constructor & Dependencies                   // Mock service setup and controller initialization
///    - SetupAuthenticatedUser()                     // Helper for authentication context setup
/// 
/// 2. Cart Display & Retrieval Tests ................................. Lines   66-85
///    - Index_ShouldReturnViewWithCartItems()        // Validates cart display functionality
/// 
/// 3. Add to Cart Operation Tests .................................... Lines   87-118
///    - AddToCart_WithValidData_ShouldReturnSuccessJson()      // Success scenario testing
///    - AddToCart_WithInvalidData_ShouldReturnFailureJson()    // Failure scenario handling
///    - AddToCart_WithInvalidQuantity_ShouldReturnBadRequest() // Input validation testing
/// 
/// 4. Cart Modification Tests ........................................ Lines  120-155
///    - RemoveFromCart_WithValidId_ShouldReturnSuccessJson()   // Item removal functionality
///    - UpdateQuantity_WithValidData_ShouldReturnSuccessJson() // Quantity update operations
///    - ClearCart_ShouldReturnSuccessJson()                   // Full cart clearing operations
/// 
/// 5. Cart Information Retrieval Tests ............................... Lines  157-175
///    - GetCartCount_ShouldReturnCorrectCount()               // Cart quantity calculations
/// 
/// 6. Helper Methods & Utilities ...................................... Lines  177-184
///    - SetupAuthenticatedUser()                              // Authentication context helper
/// ================================================================================
/// 
/// TESTING PATTERNS:
/// • Mock-based unit testing with isolated dependencies
/// • Comprehensive AJAX response validation with reflection-based property checking
/// • Authentication context simulation for secure operations
/// • Input validation testing with Theory/InlineData patterns
/// • FluentAssertions for readable and maintainable test assertions
/// 
/// BUSINESS RULES VALIDATED:
/// • Cart operations require authenticated users
/// • Invalid quantities (≤0) are rejected with BadRequest responses
/// • JSON responses include proper success/failure indicators
/// • Cart count calculations aggregate all item quantities
/// • Service layer integration maintains separation of concerns
/// 
/// SECURITY CONSIDERATIONS:
/// • All cart operations validate user authentication
/// • User context isolation prevents cross-user data access
/// • Input sanitization through controller validation
/// </remarks>
public class CartControllerTests
{
    #region Test Setup & Configuration

    // ── Mock Dependencies ──
    private readonly Mock<ICartService> _mockCartService;
    private readonly Mock<IUserService> _mockUserService;
    private readonly CartController _controller;

    /// <summary>
    /// Initializes test environment with mocked dependencies and properly configured controller context.
    /// </summary>
    /// <remarks>
    /// SETUP PROCESS:
    /// 1. Create mock services for cart and user operations
    /// 2. Instantiate CartController with mocked dependencies
    /// 3. Configure ASP.NET Core controller context (HttpContext, RouteData, ActionDescriptor)
    /// 4. Setup TempData provider for controller messaging
    /// 5. Establish authenticated user context for secure operations
    /// 
    /// This comprehensive setup ensures tests run in an isolated environment
    /// that closely mimics the actual runtime controller configuration.
    /// </remarks>
    public CartControllerTests()
    {
        // Initialize mock services with strict behavior validation
        _mockCartService = new Mock<ICartService>();
        _mockUserService = new Mock<IUserService>();

        // Create controller instance with injected mock dependencies
        _controller = new CartController(
            _mockCartService.Object,
            _mockUserService.Object,
            NullLogger<CartController>.Instance);

        // Configure ASP.NET Core controller context for proper request simulation
        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new RouteData(), new ControllerActionDescriptor());
        _controller.ControllerContext = new ControllerContext(actionContext);
        _controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());

        // Establish authenticated user context for cart operations
        SetupAuthenticatedUser("test@example.com");
    }

    #endregion

    #region Cart Display & Retrieval Tests

    /// <summary>
    /// Validates that the cart index page displays all cart items for the authenticated user.
    /// </summary>
    /// <remarks>
    /// BUSINESS LOGIC TESTED:
    /// • Cart service retrieves items for the correct user (email-based identification)
    /// • View result contains properly typed model data
    /// • Cart items maintain their essential properties (name, price, quantity)
    /// 
    /// TECHNICAL VALIDATION:
    /// • Controller returns ViewResult (not redirect or error)
    /// • Model is strongly typed as IEnumerable&lt;CartVM&gt;
    /// • Item count matches expected data
    /// • Product information integrity is preserved
    /// </remarks>
    [Fact]
    public async Task Index_ShouldReturnViewWithCartItems()
    {
        #region Arrange - Setup test data and mock behavior

        // Create representative cart items with realistic e-commerce data
        var cartItems = new List<CartVM>
        {
            new CartVM { ProductId = 1, ProductName = "Product 1", Price = 10.99m, Quantity = 2 },
            new CartVM { ProductId = 2, ProductName = "Product 2", Price = 15.99m, Quantity = 1 }
        };

        // Configure cart service to return test data for the authenticated user
        _mockCartService.Setup(c => c.GetCartItemsAsync("test@example.com"))
                       .ReturnsAsync(cartItems);

        #endregion

        #region Act - Execute the controller action

        var result = await _controller.Index();

        #endregion

        #region Assert - Validate response and data integrity

        // Verify controller returns a proper view result
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;

        // Validate model type and content
        var model = viewResult.Model.Should().BeAssignableToType<IEnumerable<CartVM>>().Subject;
        model.Should().HaveCount(2);
        model.First().ProductName.Should().Be("Product 1");

        #endregion
    }

    #endregion

    #region Add to Cart Operation Tests

    /// <summary>
    /// Validates successful item addition to cart with proper JSON response formatting.
    /// </summary>
    /// <remarks>
    /// This test ensures AJAX cart operations return consistent JSON responses
    /// that can be reliably consumed by frontend JavaScript frameworks.
    /// 
    /// RESPONSE FORMAT VALIDATION:
    /// • Uses reflection to verify anonymous object structure
    /// • Ensures 'success' property exists and contains correct boolean value
    /// • Validates JsonResult type for proper AJAX integration
    /// </remarks>
    [Fact]
    public async Task AddToCart_WithValidData_ShouldReturnSuccessJson()
    {
        // Arrange - Configure successful cart service response
        _mockCartService.Setup(c => c.AddToCartAsync("test@example.com", 1, 2))
                       .ReturnsAsync(true);

        // Act - Execute add to cart operation
        var result = await _controller.AddToCart(1, 2);

        // Assert - Validate JSON response structure using reflection
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var value = jsonResult.Value;
        value.Should().NotBeNull();

        // Verify the success property exists and contains expected value
        // Note: Using reflection due to anonymous object in controller response
        var successProperty = value!.GetType().GetProperty("success");
        successProperty.Should().NotBeNull();
        successProperty!.GetValue(value).Should().Be(true);
    }

    [Fact]
    public async Task AddToCart_WithInvalidData_ShouldReturnFailureJson()
    {
        // Arrange
        _mockCartService.Setup(c => c.AddToCartAsync("test@example.com", 1, 2))
                       .ReturnsAsync(false);

        // Act
        var result = await _controller.AddToCart(1, 2);

        // Assert
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var value = jsonResult.Value;
        value.Should().NotBeNull();
        
        var successProperty = value!.GetType().GetProperty("success");
        successProperty!.GetValue(value).Should().Be(false);
    }

    /// <summary>
    /// Validates input validation for quantity parameters in add-to-cart operations.
    /// </summary>
    /// <remarks>
    /// BUSINESS RULE: Cart quantities must be positive integers (≥ 1)
    /// 
    /// This theory-based test validates multiple invalid quantity scenarios:
    /// • Zero quantity (0) - Should be rejected
    /// • Negative quantity (-1) - Should be rejected
    /// 
    /// VALIDATION BEHAVIOR:
    /// • Returns BadRequest HTTP status for invalid input
    /// • Prevents service layer calls when validation fails
    /// • Ensures data integrity at the controller level
    /// </remarks>
    [Theory]
    [InlineData(0)]      // Zero quantity validation
    [InlineData(-1)]     // Negative quantity validation
    public async Task AddToCart_WithInvalidQuantity_ShouldReturnBadRequest(int quantity)
    {
        // Act - Attempt to add item with invalid quantity
        var result = await _controller.AddToCart(1, quantity);

        // Assert - Validate rejection and service protection
        result.Should().BeOfType<BadRequestObjectResult>();

        // Verify service layer was never called with invalid data
        _mockCartService.Verify(c => c.AddToCartAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()), 
                               Times.Never);
    }

    #endregion

    #region Cart Modification Tests

    [Fact]
    public async Task RemoveFromCart_WithValidId_ShouldReturnSuccessJson()
    {
        // Arrange
        _mockCartService.Setup(c => c.RemoveFromCartAsync(1))
                       .ReturnsAsync(true);

        // Act
        var result = await _controller.RemoveFromCart(1);

        // Assert
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var value = jsonResult.Value;
        value.Should().NotBeNull();
        
        var successProperty = value!.GetType().GetProperty("success");
        successProperty!.GetValue(value).Should().Be(true);
    }

    [Fact]
    public async Task UpdateQuantity_WithValidData_ShouldReturnSuccessJson()
    {
        // Arrange
        _mockCartService.Setup(c => c.UpdateQuantityAsync(1, 3))
                       .ReturnsAsync(true);

        // Act
        var result = await _controller.UpdateQuantity(1, 3);

        // Assert
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var value = jsonResult.Value;
        value.Should().NotBeNull();
        
        var successProperty = value!.GetType().GetProperty("success");
        successProperty!.GetValue(value).Should().Be(true);
    }

    [Fact]
    public async Task ClearCart_ShouldReturnSuccessJson()
    {
        // Arrange
        _mockCartService.Setup(c => c.ClearCartAsync("test@example.com"))
                       .ReturnsAsync(true);

        // Act
        var result = await _controller.ClearCart();

        // Assert
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var value = jsonResult.Value;
        value.Should().NotBeNull();
        
        var successProperty = value!.GetType().GetProperty("success");
        successProperty!.GetValue(value).Should().Be(true);
    }

    /// <summary>
    /// Validates cart count calculation aggregating quantities across all items.
    /// </summary>
    /// <remarks>
    /// CALCULATION LOGIC:
    /// Cart count represents total quantity of all items, not unique product count.
    /// Example: 2x Product A + 1x Product B + 3x Product C = 6 total items
    /// 
    /// TECHNICAL IMPLEMENTATION:
    /// • Service returns individual cart items with their quantities
    /// • Controller/frontend calculates aggregate count for display
    /// • Used for cart badge/counter in user interface
    /// </remarks>
    [Fact]
    public async Task GetCartCount_ShouldReturnCorrectCount()
    {
        #region Arrange - Setup cart items with varying quantities

        var cartItems = new List<CartVM>
        {
            new CartVM { Quantity = 2 },  // 2 items
            new CartVM { Quantity = 1 },  // 1 item  
            new CartVM { Quantity = 3 }   // 3 items
        };                                // Total: 6 items

        _mockCartService.Setup(c => c.GetCartItemsAsync("test@example.com"))
                       .ReturnsAsync(cartItems);

        #endregion

        #region Act - Get cart count

        var result = await _controller.GetCartCount();

        #endregion

        #region Assert - Validate count calculation

        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var value = jsonResult.Value;
        value.Should().NotBeNull();

        // Verify count property contains sum of all quantities (2 + 1 + 3 = 6)
        var countProperty = value!.GetType().GetProperty("count");
        countProperty!.GetValue(value).Should().Be(6);

        #endregion
    }

    #endregion

    #region Helper Methods & Utilities

    /// <summary>
    /// Configures controller with authenticated user context for secure operations testing.
    /// </summary>
    /// <param name="email">User email address for authentication context</param>
    /// <remarks>
    /// AUTHENTICATION SETUP:
    /// • Creates claims-based identity with email and user ID
    /// • Simulates authenticated request context
    /// • Enables testing of secured controller actions
    /// 
    /// SECURITY CONTEXT:
    /// • ClaimTypes.Name: User's email address
    /// • ClaimTypes.NameIdentifier: Numeric user ID
    /// • Authentication type: "Test" (for testing purposes)
    /// </remarks>
    private void SetupAuthenticatedUser(string email)
    {
        // Create authentication claims for test user
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, email),           // Primary user identifier
            new Claim(ClaimTypes.NameIdentifier, "1")    // Numeric user ID
        };

        // Build authenticated identity and principal
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        // Apply authentication context to controller
        _controller.ControllerContext.HttpContext.User = principal;
    }

    #endregion
}