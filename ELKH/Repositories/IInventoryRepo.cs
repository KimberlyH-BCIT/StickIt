using ELKH.Models;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Http;

namespace ELKH.Repositories
{
    /// <summary>
    /// Abstraction over inventory data access used by admin/staff controllers.
    /// </summary>
    public interface IInventoryRepo
    {
        Task<IEnumerable<ProductModel>> GetAllProduct();
        Task<List<ImageModel>> GetProductImages(int id);
        Task<ProductVM> EditProductQuantity(int productId, int quantityAmount);
        Task<bool> UploadImage(int productId, IFormFile file);
    }
}
