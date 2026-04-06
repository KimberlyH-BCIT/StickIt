using ELKH.Models;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Http;

namespace ELKH.Repositories
{
    /// <summary>
    /// Repository interface for inventory management operations.
    /// Defines contracts for product listing, stock adjustment, image management,
    /// category retrieval, and product review deletion.
    /// </summary>
    public interface IInventoryRepo
    {

        Task<PagedResult<InventoryVM>> GetAllProduct(string? searchString, string? sortOrder, string? stockFilter, int page = 1, int pageSize = 10);
        Task<PagedResult<InventoryVM>> GetAllProduct(string? searchString, int page = 1, int pageSize = 10);

        Task<List<ImageModel>> GetProductImages(int id);
        Task<ProductVM> EditProductQuantity(int productId, int quantityAmount);
        Task<bool> UploadImage(int productId, IFormFile file);

        Task<ProductModel> GetProductById(int Id);
        Task<List<CategoryModel>> GetAllCategories();
        Task<bool> DeleteProductReview(int reviewId);
        Task<bool> DeleteImage(int imageId);
    }
}
