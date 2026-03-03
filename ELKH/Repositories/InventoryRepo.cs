using ELKH.Data;
using ELKH.Models;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Repositories
{
    public class InventoryRepo
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        public InventoryRepo(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public async Task<IEnumerable<ProductModel>> GetAllProduct()
        {
            return await _context.Products.ToListAsync();
        }


        public async Task<List<string>> GetProductImages(int id)
        {
            return await _context.ProductImages.Where(pi => pi.FkProductId == id)
                                                            .Select(pi => pi.ProductImageURL)
                                                            .ToListAsync();
        }

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

        public async Task<bool> AddProductImage(ProductImageVM vm)
        {
            if (vm == null || vm.ProductImage == null || vm.ProductImage.Length == 0)
            {
                return false;
            }

            // Validate file extension to prevent malicious uploads
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var extension = Path.GetExtension(vm.ProductImage.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension))
            {
                return false;
            }

            // Validate content type
            var allowedContentTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };
            if (!allowedContentTypes.Contains(vm.ProductImage.ContentType.ToLowerInvariant()))
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

            // 🔥 Generate unique file name
            var fileName = Guid.NewGuid().ToString() +
                           Path.GetExtension(vm.ProductImage.FileName);

            // 🔥 Physical path
            var uploadPath = Path.Combine(_env.WebRootPath, "images");

            // 🔥 Ensure folder exists
            if (!Directory.Exists(uploadPath))
                Directory.CreateDirectory(uploadPath);

            // 🔥 Full file path
            var filePath = Path.Combine(uploadPath, fileName);

            // 🔥 Save file to disk
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
