using ELKH.Data;
using ELKH.Models;
using ELKH.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Repositories
{
    public class InventoryRepo
    {
        private readonly ApplicationDbContext _context;
        private readonly ImageStoreContext _imageDb;

        public InventoryRepo(ApplicationDbContext context, ImageStoreContext imageDb)
        {
            _context = context;
            _imageDb = imageDb;
        }

        public async Task<IEnumerable<ProductModel>> GetAllProduct()
        {
            return await _context.Products.ToListAsync();
        }

        public async Task<List<string>> GetProductImages(int id)
        {
            return await _context.ProductImages
                .Where(pi => pi.FkProductId == id)
                .Select(pi => pi.ProductImageURL)
                .ToListAsync();
        }

        public async Task<ProductVM> EditProductQuantity(int productId, int quantityAmount)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.PkProductId == productId);

            if (product == null)
                throw new NullReferenceException("Product not found");

            product.StockQuantity = quantityAmount;

            await _context.SaveChangesAsync();

            return new ProductVM
            {
                ProductId = product.PkProductId,
                ProductName = product.Name,
                Description = product.Description,
                Price = product.Price,
                StockQuantity = product.StockQuantity ?? 0,
                IsActive = product.IsActive
            };
        }

        public async Task<bool> UploadImage(int productId, IFormFile file)
        {
            if (file == null || file.Length == 0)
                return false;

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);

            var image = new ImageModel
            {
                FileName = file.FileName,
                Description = "",
                FileType = file.ContentType,
                ImageData = stream.ToArray(),
                FkProductId = productId
            };

            _imageDb.Images.Add(image);

            return await _imageDb.SaveChangesAsync() > 0;
        }

        public async Task<bool> AddProductImage(ProductImageVM vm)
        {
            if (vm == null || vm.ProductImage == null)
                return false;

            const long maxBytes = 10 * 1024 * 1024;

            var allowedTypes = new HashSet<string>
            {
                "image/jpeg",
                "image/png",
                "image/gif",
                "image/webp"
            };

            if (!allowedTypes.Contains(vm.ProductImage.ContentType))
                return false;

            if (vm.ProductImage.Length > maxBytes)
                return false;

            using var stream = new MemoryStream();
            await vm.ProductImage.CopyToAsync(stream);

            var image = new ImageModel
            {
                FileName = vm.ProductImage.FileName,
                Description = "",
                FileType = vm.ProductImage.ContentType,
                ImageData = stream.ToArray(),
                FkProductId = vm.FkProductId
            };

            _imageDb.Images.Add(image);

            return await _imageDb.SaveChangesAsync() > 0;
        }
    }
}