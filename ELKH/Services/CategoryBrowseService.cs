using ELKH.Models;
using ELKH.ViewModels;

namespace ELKH.Services;

public sealed class CategoryBrowseService(IProductService productService) : ICategoryBrowseService
{
    public async Task<CategoryBrowseResultVM?> GetProductsByCategoryAsync(int categoryId, int page, string sort, int pageSize = 12, CancellationToken ct = default)
    {
        var categories = (await productService.GetCategoriesAsync(ct)).ToList();
        var currentCategory = categories.FirstOrDefault(c => c.PkCategoryId == categoryId);
        if (currentCategory == null)
        {
            return null;
        }

        var items = (await productService.GetAllAsync(ct)).Where(p => p.CategoryId == categoryId);
        return BuildResult(categoryId, page, sort, pageSize, categories, currentCategory, items, isPromotionView: false);
    }

    public async Task<CategoryBrowseResultVM?> GetPromotionalProductsByCategoryAsync(int categoryId, int page, string sort, int pageSize = 12, CancellationToken ct = default)
    {
        var categories = (await productService.GetCategoriesAsync(ct)).ToList();
        var currentCategory = categories.FirstOrDefault(c => c.PkCategoryId == categoryId);
        if (currentCategory == null)
        {
            return null;
        }

        var items = (await productService.GetPromotionalProductsAsync(ct)).Where(p => p.CategoryId == categoryId);
        return BuildResult(categoryId, page, sort, pageSize, categories, currentCategory, items, isPromotionView: true);
    }

    public async Task<CategoryBrowseResultVM> GetPromotionalProductsAsync(int page, string sort, int pageSize = 12, CancellationToken ct = default)
    {
        var categories = (await productService.GetCategoriesAsync(ct)).ToList();
        var items = await productService.GetPromotionalProductsAsync(ct);
        return BuildPromotionsResult(page, sort, pageSize, categories, items);
    }

    public async Task<IReadOnlyList<CategoryProductCountVM>> GetCategoryCountsAsync(CancellationToken ct = default)
    {
        var categories = (await productService.GetCategoriesAsync(ct)).ToList();
        var allProducts = (await productService.GetAllAsync(ct)).ToList();

        return categories.Select(category => new CategoryProductCountVM
        {
            Category = category,
            ProductCount = allProducts.Count(p => p.CategoryId == category.PkCategoryId)
        }).ToList();
    }

    private static CategoryBrowseResultVM BuildResult(
        int categoryId,
        int page,
        string sort,
        int pageSize,
        IReadOnlyList<CategoryModel> categories,
        CategoryModel currentCategory,
        IEnumerable<ProductVM> items,
        bool isPromotionView)
    {
        var ordered = ApplySorting(items, sort, isPromotionView).ToList();
        var totalPages = Math.Max(1, (int)Math.Ceiling(ordered.Count / (double)pageSize));
        var currentPage = Math.Clamp(page, 1, totalPages);
        var pageItems = ordered.Skip((currentPage - 1) * pageSize).Take(pageSize).ToList();

        return new CategoryBrowseResultVM
        {
            CurrentCategory = currentCategory,
            Categories = categories.ToList(),
            Items = pageItems,
            Page = currentPage,
            TotalPages = totalPages,
            Total = ordered.Count,
            Sort = sort
        };
    }

    private static CategoryBrowseResultVM BuildPromotionsResult(
        int page,
        string sort,
        int pageSize,
        IReadOnlyList<CategoryModel> categories,
        IEnumerable<ProductVM> items)
    {
        var ordered = ApplySorting(items, sort, isPromotionView: true).ToList();
        var totalPages = Math.Max(1, (int)Math.Ceiling(ordered.Count / (double)pageSize));
        var currentPage = Math.Clamp(page, 1, totalPages);
        var pageItems = ordered.Skip((currentPage - 1) * pageSize).Take(pageSize).ToList();

        return new CategoryBrowseResultVM
        {
            Categories = categories.ToList(),
            Items = pageItems,
            Page = currentPage,
            TotalPages = totalPages,
            Total = ordered.Count,
            Sort = sort
        };
    }

    private static IEnumerable<ProductVM> ApplySorting(IEnumerable<ProductVM> items, string sort, bool isPromotionView)
    {
        return isPromotionView
            ? sort switch
            {
                "name_desc" => items.OrderByDescending(p => p.ProductName, StringComparer.OrdinalIgnoreCase),
                "price_low" => items.OrderBy(p => p.Price),
                "price_high" => items.OrderByDescending(p => p.Price),
                "discount_high" => items.OrderByDescending(p => p.DiscountPercent).ThenBy(p => p.ProductName),
                "newest" => items.OrderByDescending(p => p.DateAdded),
                "oldest" => items.OrderBy(p => p.DateAdded),
                "name_asc" => items.OrderBy(p => p.ProductName, StringComparer.OrdinalIgnoreCase),
                _ => items.OrderByDescending(p => p.DiscountPercent).ThenBy(p => p.ProductName)
            }
            : sort switch
            {
                "name_desc" => items.OrderByDescending(p => p.ProductName, StringComparer.OrdinalIgnoreCase),
                "price_low" => items.OrderBy(p => p.Price),
                "price_high" => items.OrderByDescending(p => p.Price),
                "newest" => items.OrderByDescending(p => p.DateAdded),
                "oldest" => items.OrderBy(p => p.DateAdded),
                _ => items.OrderBy(p => p.ProductName, StringComparer.OrdinalIgnoreCase)
            };
    }
}
