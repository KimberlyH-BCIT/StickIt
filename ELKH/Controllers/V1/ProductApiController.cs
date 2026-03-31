using Microsoft.AspNetCore.Mvc;
using ELKH.Services;
using ELKH.ViewModels;
using ELKH.Models.Api;

namespace ELKH.Controllers.V1;

/// <summary>
/// Product API Controller - Version 1.0
/// Provides product catalog functionality via RESTful API endpoints.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public class ProductApiController : ControllerBase
{
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
            filtered = sort.ToLower() switch
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

    /// <summary>
    /// Get a specific product by ID
    /// </summary>
    /// <param name="id">Product ID</param>
    /// <returns>Product details</returns>
    [HttpGet("{id:int}")]
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

    /// <summary>
    /// Search products with suggestions
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="limit">Maximum number of suggestions (default: 10, max: 20)</param>
    /// <returns>List of search suggestions</returns>
    [HttpGet("search-suggestions")]
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
}