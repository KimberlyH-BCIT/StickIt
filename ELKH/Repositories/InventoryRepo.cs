using ELKH.Data;
using ELKH.Models;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static System.Net.Mime.MediaTypeNames;

namespace ELKH.Repositories
{
    /// <summary>
    /// Repository for inventory management operations including product queries,
    /// stock quantity updates, and product image uploads.
    /// </summary>
    public class InventoryRepo
    {
        private readonly ApplicationDbContext _context;
        private readonly ImageStoreContext _imageDb;
        public InventoryRepo(ApplicationDbContext context, ImageStoreContext imageDb)
        {
            _context = context;
            _imageDb = imageDb;
        }

        /// <summary>Returns all products without any includes (lightweight listing query).</summary>
        public async Task<IEnumerable<ProductModel>> GetAllProduct()
        {
            return await _context.Products.ToListAsync();
        }


        public async Task<List<ImageModel>> GetProductImages(int id)
        {
            return await _imageDb.Images.Where(pi => pi.FkProductId == id)
                                               .ToListAsync();
        }

        /// <summary>
        /// Updates the stock quantity for a single product and returns the updated view model.
        /// Throws <see cref="NullReferenceException"/> if no product with <paramref name="productId"/> exists.
        /// </summary>
        public async Task<ProductVM> EditProductQuantity(int productId, int quantityAmount)
        {
            var products = await _context.Products.Where(p => p.PkProductId == productId)
                                                  .FirstOrDefaultAsync();
            products.StockQuantity = quantityAmount;

            await _context.SaveChangesAsync();

            var vm = new ProductVM
            {
                ProductId = products.PkProductId,
                ProductName = products.Name,
                Description = products.Description,
                Price = products.Price,
                StockQuantity = products.StockQuantity,
                IsActive = products.IsActive
            };

            return vm;
        }
        public async Task<bool> UploadImage(int productId, IFormFile file)
        {
            if (file != null && file.Length > 0)
            {
                // Convert the file to a byte array.
                using var stream = new MemoryStream();
                file.CopyTo(stream);
                var imageBytes = stream.ToArray();

                // Create a new Image instance.
                var image = new ImageModel
                {
                    FileName = file.FileName,
                    Description = "",
                    FileType = file.ContentType,
                    ImageData = imageBytes,
                    FkProductId = productId
                };

                // Add to database context and save.
                _imageDb.Images.Add(image);
                bool isSaved = _imageDb.SaveChanges() > 0;
                if (!isSaved)
                {
                    return false;
                }
                return true;
            }
            return false;
        }

        public async Task<bool> DeleteImage(int imageId)
        {
            var findImageById = await _imageDb.Images.Where(i => i.ImageId == imageId)
                                               .FirstOrDefaultAsync();
            if(findImageById == null)
            {
                return false;
            }

            _imageDb.Images.Remove(findImageById);
            _imageDb.SaveChanges();
            return true;
        }
    }
} 

