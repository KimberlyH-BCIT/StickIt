using ELKH.ViewModels;

namespace ELKH.Services;

public interface ICategoryBrowseService
{
    Task<CategoryBrowseResultVM?> GetProductsByCategoryAsync(int categoryId, int page, string sort, int pageSize = 12, CancellationToken ct = default);

    Task<CategoryBrowseResultVM?> GetPromotionalProductsByCategoryAsync(int categoryId, int page, string sort, int pageSize = 12, CancellationToken ct = default);

    Task<CategoryBrowseResultVM> GetPromotionalProductsAsync(int page, string sort, int pageSize = 12, CancellationToken ct = default);

    Task<IReadOnlyList<CategoryProductCountVM>> GetCategoryCountsAsync(CancellationToken ct = default);
}
