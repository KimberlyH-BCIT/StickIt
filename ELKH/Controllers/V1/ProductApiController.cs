using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ELKH.Services;
using ELKH.ViewModels;
using ELKH.Models.Api;
using System.Globalization;

namespace ELKH.Controllers.V1;

// ╔===============================================================================================╗
// ║                        PRODUCT API CONTROLLER - TABLE OF CONTENTS                            ║
// ╚===============================================================================================╝
// 
// OVERVIEW:
// RESTful API controller providing comprehensive product catalog functionality with support
// for filtering, pagination, search, and availability checking through standardized endpoints.
// 
// TABLE OF CONTENTS:
// ┌─ Section 1: Controller Setup & Dependencies .......................................... Line 57
// │  ├─ Constructor with dependency injection
// │  ├─ Service integrations (IProductService, ISearchService)
// │  └─ API versioning and routing configuration
// ├─ Section 2: Product Catalog Operations .............................................. Line 59
// │  ├─ GetProducts() - Retrieve paginated product list with filtering/sorting
// │  ├─ Parameter validation (page size limits, sort options)
// │  ├─ Search integration with ISearchService
// │  ├─ Category filtering and advanced sorting logic
// │  └─ Comprehensive pagination with metadata
// ├─ Section 3: Single Product Operations ............................................... Line 140
// │  ├─ GetProduct() - Retrieve individual product by ID
// │  ├─ Product-to-API model transformation
// │  ├─ Not found handling with standardized errors
// │  └─ Full product detail serialization
// ├─ Section 4: Search & Discovery Services ............................................. Line 200
// │  ├─ GetSearchSuggestions() - Autocomplete/typeahead functionality
// │  ├─ Query validation and limit enforcement
// │  ├─ Search service integration for name matching
// │  └─ Optimized suggestion retrieval
// └─ Section 5: Inventory & Availability Management ..................................... Line 248
//    ├─ CheckAvailability() - Real-time stock and availability checking
//    ├─ Stock status classification (In Stock, Low Stock, Out of Stock)
//    ├─ Low stock threshold management (< 10 items)
//    └─ Availability model with comprehensive status information
//
// ARCHITECTURE NOTES:
// • RESTful API design following OpenAPI/Swagger standards
// • Versioned endpoints with v1.0 API versioning strategy
// • Standardized response models (ApiResponse<T>, ApiErrorResponse)
// • Comprehensive error handling with structured error codes
// • Pagination support with configurable limits and metadata
//
// API DESIGN PATTERNS:
// • Resource-based URL patterns (/api/v1.0/ProductApi)
// • HTTP verbs for semantic actions (GET for retrieval operations)
// • Query parameters for filtering and configuration
// • Consistent response envelope with success/error patterns
// • Comprehensive Swagger documentation with response types
//
// BUSINESS LOGIC:
// • Product catalog browsing with advanced filtering capabilities
// • Search integration for product discovery and suggestions
// • Real-time inventory status and availability checking
// • Category-based filtering for organized product browsing
// • Price-based and date-based sorting for user preferences
//
// PERFORMANCE CONSIDERATIONS:
// • Page size limits prevent excessive data transfer (max 100 items)
// • Search service integration for optimized product discovery
// • Lazy loading and filtering applied at service layer
// • Efficient pagination with skip/take operations
// • Minimal API model transformation for reduced payload
//
// SECURITY IMPLEMENTATION:
// • Public API endpoints (no authentication required for catalog browsing)
// • Input validation on all parameters (pagination, search queries)
// • Parameter sanitization and bounds checking
// • Error message standardization to prevent information disclosure
// • Structured logging for audit and monitoring

/// <summary>
/// Product API Controller - Version 1.0
/// Provides product catalog functionality via RESTful API endpoints.
/// </summary>
/// <remarks>
/// <para><strong>API Version 1.0 Endpoints</strong></para>
/// This controller provides comprehensive product catalog access through RESTful endpoints
/// with support for advanced filtering, search, pagination, and real-time availability checking.
/// 
/// <para><strong>Supported Operations:</strong></para>
/// <list type="bullet">
/// <item>Product catalog browsing with filtering and pagination</item>
/// <item>Individual product detail retrieval</item>
/// <item>Search suggestions and autocomplete functionality</item>
/// <item>Real-time inventory and availability status</item>
/// <item>Category-based filtering and sorting options</item>
/// </list>
/// 
/// <para><strong>Response Format:</strong></para>
/// All endpoints return standardized JSON responses using ApiResponse&lt;T&gt; envelope pattern
/// with consistent success/error handling and structured error codes for client integration.
/// </remarks>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public class ProductApiController : ControllerBase
{
    #region Section 1: Controller Setup & Dependencies

    // ===================================================================
    // Section 1: Controller Setup & Dependencies
    // ===================================================================

    private readonly IProductService _productService;
    private readonly ISearchService _searchService;
    private readonly ILogger<ProductApiController> _logger;

    public ProductApiController(
        IProductService productService,
        ISearchService searchService,
        ILogger<ProductApiController> logger)
    {
        _productService = productService;
        _searchService = searchService;
        _logger = logger;
    }

    #endregion

    #region Section 2: Product Catalog Operations

    // ===================================================================
    // Section 2: Product Catalog Operations
    // ===================================================================

    /// <summary>
    /// Get all products with optional filtering and pagination
    /// </summary>
    /// <param name="search">Search term for product name or description</param>
    /// <param name="categoryId">Filter by category ID</param>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Items per page (default: 20, max: 100)</param>
    /// <param name="sort">Sort order (name_asc, name_desc, price_low, price_high, newest, oldest)</param>
    /// <returns>Paginated list of products</returns>
    [HttpGet]
    [ResponseCache(CacheProfileName = "ProductCatalog")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ProductApiModel>>), 200)]
    [ProducesResponseType(typeof(ApiErrorResponse), 400)]
    public async Task<IActionResult> GetProducts(
        [FromQuery] string? search = null,
        [FromQuery] int? categoryId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string sort = "name_asc")
    {
        try
        {
            // Validate pagination parameters
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100; // Prevent excessive page sizes

            var allProducts = await _productService.GetAllAsync();

            // Apply search filtering
            var filtered = allProducts.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchResults = await _searchService.SearchNames(search);
                var searchIds = searchResults.Select(r => r.Id).ToHashSet();
                filtered = filtered.Where(p => searchIds.Contains(p.ProductId));
            }

            // Apply category filtering
            if (categoryId.HasValue)
            {
                filtered = filtered.Where(p => p.CategoryId == categoryId.Value);
            }

            // Apply sorting
            filtered = sort.ToLower(CultureInfo.InvariantCulture) switch
            {
                "name_desc" => filtered.OrderByDescending(p => p.ProductName),
                "price_low" => filtered.OrderBy(p => p.Price),
                "price_high" => filtered.OrderByDescending(p => p.Price),
                "newest" => filtered.OrderByDescending(p => p.DateAdded),
                "oldest" => filtered.OrderBy(p => p.DateAdded),
                _ => filtered.OrderBy(p => p.ProductName) // Default: name_asc
            };

            // Apply pagination
            var totalCount = filtered.Count();
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            var pagedItems = filtered.Skip((page - 1) * pageSize).Take(pageSize);

            var apiProducts = pagedItems.Select(p => new ProductApiModel
            {
                Id = p.ProductId,
                Name = p.ProductName,
                Description = p.Description,
                PriceInCents = (int)(p.Price * 100), // Convert to cents
                Price = p.Price,
                CategoryId = p.CategoryId,
                CategoryName = p.CategoryName,
                Stock = p.StockQuantity ?? 0,
                StockQuantity = p.StockQuantity ?? 0,
                IsAvailable = p.IsInStock && p.IsActive,
                CreatedAt = p.DateAdded,
                UpdatedAt = p.DateAdded
            }).ToList();

            var result = new PagedResult<ProductApiModel>
            {
                Items = apiProducts,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            };

            return Ok(new ApiResponse<PagedResult<ProductApiModel>>
            {
                Data = result,
                Success = true,
                Message = "Products retrieved successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving products");
            return BadRequest(new ApiErrorResponse
            {
                Success = false,
                Message = "Failed to retrieve products",
                ErrorCode = "PRODUCTS_RETRIEVAL_ERROR"
            });
        }
    }

    #endregion

    #region Section 3: Single Product Operations

    // ===================================================================
    // Section 3: Single Product Operations
    // ===================================================================

    /// <summary>
    /// Get a specific product by ID
    /// </summary>
    /// <param name="id">Product ID</param>
    /// <returns>Product details</returns>
    [HttpGet("{id:int}")]
    [ResponseCache(CacheProfileName = "ProductDetails")]
    [ProducesResponseType(typeof(ApiResponse<ProductApiModel>), 200)]
    [ProducesResponseType(typeof(ApiErrorResponse), 404)]
    public async Task<IActionResult> GetProduct(int id)
    {
        try
        {
            var product = await _productService.GetByIdAsync(id);

            if (product == null)
            {
                return NotFound(new ApiErrorResponse
                {
                    Success = false,
                    Message = $"Product with ID {id} not found",
                    ErrorCode = "PRODUCT_NOT_FOUND"
                });
            }

            var apiProduct = new ProductApiModel
            {
                Id = product.ProductId,
                Name = product.ProductName,
                Description = product.Description,
                Price = product.Price,
                PriceInCents = (int)(product.Price * 100),
                StockQuantity = product.StockQuantity ?? 0,
                Stock = product.StockQuantity ?? 0,
                CategoryId = product.CategoryId,
                CategoryName = product.CategoryName,
                IsAvailable = product.IsInStock && product.IsActive,
                CreatedAt = product.DateAdded,
                UpdatedAt = product.DateAdded
            };

            return Ok(new ApiResponse<ProductApiModel>
            {
                Data = apiProduct,
                Success = true,
                Message = "Product retrieved successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving product {ProductId}", id);
            return BadRequest(new ApiErrorResponse
            {
                Success = false,
                Message = "Failed to retrieve product",
                ErrorCode = "PRODUCT_RETRIEVAL_ERROR"
            });
        }
    }

    #endregion

    #region Section 4: Search & Discovery Services

    // ===================================================================
    // Section 4: Search & Discovery Services
    // ===================================================================

    /// <summary>
    /// Search products with suggestions
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="limit">Maximum number of suggestions (default: 10, max: 20)</param>
    /// <returns>List of search suggestions</returns>
    [HttpGet("search-suggestions")]
    [ResponseCache(CacheProfileName = "SearchResults")]
    [ProducesResponseType(typeof(ApiResponse<List<string>>), 200)]
    public async Task<IActionResult> GetSearchSuggestions(
        [FromQuery] string query,
        [FromQuery] int limit = 10)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Ok(new ApiResponse<List<string>>
                {
                    Data = new List<string>(),
                    Success = true,
                    Message = "No suggestions for empty query"
                });
            }

            if (limit < 1) limit = 10;
            if (limit > 20) limit = 20;

            var searchResults = await _searchService.SearchNames(query);
            var suggestions = searchResults.Take(limit).Select(r => r.Name).ToList();

            return Ok(new ApiResponse<List<string>>
            {
                Data = suggestions.ToList(),
                Success = true,
                Message = "Search suggestions retrieved successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting search suggestions for query: {Query}", query);
            return BadRequest(new ApiErrorResponse
            {
                Success = false,
                Message = "Failed to retrieve search suggestions",
                ErrorCode = "SEARCH_SUGGESTIONS_ERROR"
            });
        }
    }

    #endregion

    #region Section 5: Inventory & Availability Management

    // ===================================================================
    // Section 5: Inventory & Availability Management
    // ===================================================================

    /// <summary>
    /// Check product availability and stock status
    /// </summary>
    /// <param name="id">Product ID</param>
    /// <returns>Availability information</returns>
    [HttpGet("{id:int}/availability")]
    [ProducesResponseType(typeof(ApiResponse<ProductAvailabilityModel>), 200)]
    [ProducesResponseType(typeof(ApiErrorResponse), 404)]
    public async Task<IActionResult> CheckAvailability(int id)
    {
        try
        {
            var product = await _productService.GetByIdAsync(id);

            if (product == null)
            {
                return NotFound(new ApiErrorResponse
                {
                    Success = false,
                    Message = $"Product with ID {id} not found",
                    ErrorCode = "PRODUCT_NOT_FOUND"
                });
            }

            var availability = new ProductAvailabilityModel
            {
                ProductId = product.ProductId,
                IsAvailable = product.IsInStock && product.IsActive,
                StockQuantity = product.StockQuantity ?? 0,
                IsLowStock = (product.StockQuantity ?? 0) < 10,
                StockStatus = (product.StockQuantity ?? 0) switch
                {
                    0 => "Out of Stock",
                    < 10 => "Low Stock",
                    _ => "In Stock"
                }
            };

            return Ok(new ApiResponse<ProductAvailabilityModel>
            {
                Data = availability,
                Success = true,
                Message = "Product availability checked successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking availability for product {ProductId}", id);
            return BadRequest(new ApiErrorResponse
            {
                Success = false,
                Message = "Failed to check product availability",
                ErrorCode = "AVAILABILITY_CHECK_ERROR"
            });
        }
    }

    #endregion
}
