using ELKH.Data;
using ELKH.Models;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Repositories
{
    /// <summary>
    /// Repository for inventory management operations including product queries,
    /// stock quantity updates, and product image uploads.
    /// </summary>
    public class InventoryRepo
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public InventoryRepo(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        /// <summary>Returns all products without any includes (lightweight listing query).</summary>
        public async Task<IEnumerable<ProductModel>> GetAllProduct()
        {
            return await _context.Products.ToListAsync();
        }

        /// <summary>Returns the URL strings for all images associated with the given product.</summary>
        public async Task<List<string>> GetProductImages(int id)
        {
            return await _context.ProductImages.Where(pi => pi.FkProductId == id)
                                               .Select(pi => pi.ProductImageURL)
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
                StockQuantity = products.StockQuantity ?? 0,
                IsActive = products.IsActive
            };

            return vm;
        }

        /// <summary>
        /// Saves an uploaded product image to <c>wwwroot/images</c> and records its URL in the database.
        /// Returns <c>false</c> when the VM, file, or target product is missing.
        /// </summary>
        public async Task<bool> AddProductImage(ProductImageVM vm)
        {
            if (vm == null || vm.ProductImage == null || vm.ProductImage.Length == 0)
            {
                return false;
            }

            var product = await _context.Products
                .Include(p => p.ProductImages)
                .FirstOrDefaultAsync(p => p.PkProductId == vm.FkProductId);

            if (product == null)
            {
                return false;
            }

            // Use a GUID-based file name to prevent collisions and avoid exposing
            // the original upload name (which could contain path traversal characters).
            var fileName = Guid.NewGuid().ToString() +
                           Path.GetExtension(vm.ProductImage.FileName);

            // Resolve the physical path to wwwroot/images at runtime via IWebHostEnvironment.
            var uploadPath = Path.Combine(_env.WebRootPath, "images");

            // Create the images directory on first use if it does not already exist.
            if (!Directory.Exists(uploadPath))
                Directory.CreateDirectory(uploadPath);

            var filePath = Path.Combine(uploadPath, fileName);

            // Stream the upload directly to disk to avoid holding the entire file in memory.
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await vm.ProductImage.CopyToAsync(stream);
            }

            // Create DB record - set the required navigation property 'Product'
            var image = new ProductImageModel
            {
                ProductImageURL = "/images/" + fileName,
                FkProductId = product.PkProductId,
                Product = product
            };

            product.ProductImages.Add(image);

            await _context.SaveChangesAsync();

            return true;
        }
    }
}
