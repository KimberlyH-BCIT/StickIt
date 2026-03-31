using Microsoft.AspNetCore.Mvc;
using ELKH.Services;
using ELKH.ViewModels;
using ELKH.Models.Api;

namespace ELKH.Controllers.V2;

/// <summary>
/// Product API Controller - Version 2.0
/// Enhanced product catalog functionality with improved response structure and additional features.
/// </summary>
[ApiController]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public class ProductApiController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly ISearchService _searchService;
    private readonly IRatingService _ratingService;
    private readonly ILogger<ProductApiController> _logger;

    public ProductApiController(
        IProductService productService,
        ISearchService searchService,
        IRatingService ratingService,
        ILogger<ProductApiController> logger)
    {
        _productService = productService;
        _searchService = searchService;
        _ratingService = ratingService;
        _logger = logger;
    }

    /// <summary>
    /// Get all products with enhanced filtering and pagination (v2.0)
    /// </summary>
    /// <param name="search">Search term for product name or description</param>
    /// <param name="categoryId">Filter by category ID</param>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Items per page (default: 20, max: 100)</param>
    /// <param name="sort">Sort order (name_asc, name_desc, price_low, price_high, newest, oldest, rating)</param>
    /// <param name="priceMin">Minimum price filter</param>
    /// <param name="priceMax">Maximum price filter</param>
    /// <param name="inStock">Filter for in-stock items only</param>
    /// <returns>Enhanced paginated list of products with metadata</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ProductApiV2Model>>), 200)]
    [ProducesResponseType(typeof(ApiErrorResponse), 400)]
    public async Task<IActionResult> GetProducts(
        [FromQuery] string? search = null,
        [FromQuery] int? categoryId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string sort = "name_asc",
        [FromQuery] decimal? priceMin = null,
        [FromQuery] decimal? priceMax = null,
        [FromQuery] bool? inStock = null)
    {
        try
        {
            // Validate pagination parameters
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            var allProducts = await _productService.GetAllAsync();

            // Apply filters
            var filtered = allProducts.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchResults = await _searchService.SearchNames(search);
                var searchIds = searchResults.Select(r => r.Id).ToHashSet();
                filtered = filtered.Where(p => searchIds.Contains(p.ProductId));
            }

            if (categoryId.HasValue)
            {
                filtered = filtered.Where(p => p.CategoryId == categoryId.Value);
            }

            if (priceMin.HasValue)
            {
                filtered = filtered.Where(p => p.Price >= priceMin.Value);
            }

            if (priceMax.HasValue)
            {
                filtered = filtered.Where(p => p.Price <= priceMax.Value);
            }

            if (inStock == true)
            {
                filtered = filtered.Where(p => p.IsInStock);
            }

            // Apply sorting
            filtered = sort.ToLower() switch
            {
                "name_desc" => filtered.OrderByDescending(p => p.ProductName),
                "price_low" => filtered.OrderBy(p => p.Price),
                "price_high" => filtered.OrderByDescending(p => p.Price),
                "newest" => filtered.OrderByDescending(p => p.DateAdded),
                "oldest" => filtered.OrderBy(p => p.DateAdded),
                _ => filtered.OrderBy(p => p.ProductName) 
            };

            // Apply pagination
            var totalCount = filtered.Count();
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            var pagedItems = filtered.Skip((page - 1) * pageSize).Take(pageSize);

            var apiProducts = new List<ProductApiV2Model>();

            foreach (var product in pagedItems)
            {
                // Get approved reviews for rating calculation
                var reviews = await _ratingService.GetApprovedReviewsAsync(product.ProductId);

                var apiProduct = new ProductApiV2Model
                {
                    Id = product.ProductId,
                    Name = product.ProductName,
                    Description = product.Description,
                    PriceInCents = (int)(product.Price * 100),
                    OriginalPriceInCents = (int)(product.Price * 100),
                    DiscountPercent = product.DiscountPercent,
                    Category = new ProductCategoryInfo
                    {
                        Id = product.CategoryId,
                        Name = product.CategoryName
                    },
                    Stock = new ProductStockInfo
                    {
                        Quantity = product.StockQuantity ?? 0,
                        IsAvailable = product.IsInStock && product.IsActive,
                        IsLowStock = (product.StockQuantity ?? 0) < 10,
                        Status = (product.StockQuantity ?? 0) switch
                        {
                            0 => "out_of_stock",
                            < 10 => "low_stock",
                            _ => "in_stock"
                        }
                    },
                    Rating = new ProductRatingSummary
                    {
                        Average = reviews.Any() ? (decimal)reviews.Average(r => r.Rating) : 0,
                        Count = reviews.Count,
                        Distribution = reviews.GroupBy(r => r.Rating)
                                           .ToDictionary(g => g.Key, g => g.Count())
                    },
                    Tags = new List<string>(),
                    Timestamps = new ProductTimestamps
                    {
                        CreatedAt = product.DateAdded,
                        UpdatedAt = product.DateAdded
                    }
                };

                // Add badges based on product properties
                if (product.IsBestSeller) apiProduct.Tags.Add("bestseller");
                if (product.IsTrending) apiProduct.Tags.Add("trending");
                if (product.IsNewArrival) apiProduct.Tags.Add("new_arrival");
                if (product.DiscountPercent > 0) apiProduct.Tags.Add("on_sale");

                apiProducts.Add(apiProduct);
            }

            var result = new PagedResult<ProductApiV2Model>
            {
                Items = apiProducts,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            };

            return Ok(new ApiResponse<PagedResult<ProductApiV2Model>>
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
    /// Get a specific product by ID with enhanced details (v2.0)
    /// </summary>
    /// <param name="id">Product ID</param>
    /// <param name="includeReviews">Include customer reviews</param>
    /// <param name="includeRelated">Include related products</param>
    /// <returns>Enhanced product details</returns>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<ProductApiV2Model>), 200)]
    [ProducesResponseType(typeof(ApiErrorResponse), 404)]
    public async Task<IActionResult> GetProduct(
        int id,
        [FromQuery] bool includeReviews = false,
        [FromQuery] bool includeRelated = false)
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

            var reviews = await _ratingService.GetApprovedReviewsAsync(id);

            var apiProduct = new ProductApiV2Model
            {
                Id = product.ProductId,
                Name = product.ProductName,
                Description = product.Description,
                PriceInCents = (int)(product.Price * 100),
                OriginalPriceInCents = (int)(product.Price * 100),
                DiscountPercent = product.DiscountPercent,
                Category = new ProductCategoryInfo
                {
                    Id = product.CategoryId,
                    Name = product.CategoryName
                },
                Stock = new ProductStockInfo
                {
                    Quantity = product.StockQuantity ?? 0,
                    IsAvailable = product.IsInStock && product.IsActive,
                    IsLowStock = (product.StockQuantity ?? 0) < 10,
                    Status = (product.StockQuantity ?? 0) switch
                    {
                        0 => "out_of_stock",
                        < 10 => "low_stock",
                        _ => "in_stock"
                    }
                },
                Rating = new ProductRatingSummary
                {
                    Average = reviews.Any() ? (decimal)reviews.Average(r => r.Rating) : 0,
                    Count = reviews.Count,
                    Distribution = reviews.GroupBy(r => r.Rating)
                                       .ToDictionary(g => g.Key, g => g.Count())
                },
                Tags = new List<string>(),
                Timestamps = new ProductTimestamps
                {
                    CreatedAt = product.DateAdded,
                    UpdatedAt = product.DateAdded
                }
            };

            // Add badges based on product properties
            if (product.IsBestSeller) apiProduct.Tags.Add("bestseller");
            if (product.IsTrending) apiProduct.Tags.Add("trending");
            if (product.IsNewArrival) apiProduct.Tags.Add("new_arrival");
            if (product.DiscountPercent > 0) apiProduct.Tags.Add("on_sale");

            return Ok(new ApiResponse<ProductApiV2Model>
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
}

public class FiltersAppliedModel
{
    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public decimal? PriceMin { get; set; }
    public decimal? PriceMax { get; set; }
    public bool? InStockOnly { get; set; }
    public string? SortBy { get; set; }
}

public class ApiResponseV2<T>
{
    public T? Data { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Version { get; set; } = "2.0";
    public DateTime Timestamp { get; set; }
    public string RequestId { get; set; } = string.Empty;
}

public class ApiErrorResponseV2
{
    public bool Success { get; set; } = false;
    public string Message { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string Version { get; set; } = "2.0";
    public DateTime Timestamp { get; set; }
    public string RequestId { get; set; } = string.Empty;
}