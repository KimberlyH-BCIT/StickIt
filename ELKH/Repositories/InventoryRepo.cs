using ELKH.Data;
using ELKH.Models;
using ELKH.ViewModels;
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
        /// </summary>
        /// <exception cref="KeyNotFoundException">Thrown when no product with <paramref name="productId"/> exists.</exception>
        public async Task<ProductVM> EditProductQuantity(int productId, int quantityAmount)
        {
            var product = await _context.Products
                                        .Where(p => p.PkProductId == productId)
                                        .FirstOrDefaultAsync()
                         ?? throw new KeyNotFoundException($"Product {productId} not found.");

            product.StockQuantity = quantityAmount;
            await _context.SaveChangesAsync();

            return new ProductVM
            {
                ProductId     = product.PkProductId,
                ProductName   = product.Name,
                Description   = product.Description,
                Price         = product.Price,
                StockQuantity = product.StockQuantity,
                IsActive      = product.IsActive
            };
        }
        private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

        private static readonly HashSet<string> s_allowedExtensions =
            new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" };

        private static bool HasValidImageSignature(byte[] bytes)
        {
            if (bytes.Length < 4) return false;

            // JPEG: FF D8 FF
            if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
                return true;
            // PNG: 89 50 4E 47 0D 0A 1A 0A
            if (bytes.Length >= 8 &&
                bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
                bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
                return true;
            // GIF: GIF8
            if (bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x38)
                return true;
            // BMP: BM
            if (bytes[0] == 0x42 && bytes[1] == 0x4D)
                return true;
            // WebP: RIFF....WEBP
            if (bytes.Length >= 12 &&
                bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
                bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
                return true;

            return false;
        }

        public async Task<bool> UploadImage(int productId, IFormFile file)
        {
            if (file == null || file.Length == 0) return false;
            if (file.Length > MaxFileSizeBytes) return false;

            var ext = Path.GetExtension(file.FileName);
            if (!s_allowedExtensions.Contains(ext)) return false;

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            var imageBytes = stream.ToArray();

            if (!HasValidImageSignature(imageBytes)) return false;

            var safeFileName = Guid.NewGuid().ToString("N") + ext.ToLowerInvariant();

            var image = new ImageModel
            {
                FileName = safeFileName,
                Description = "",
                FileType = file.ContentType,
                ImageData = imageBytes,
                FkProductId = productId
            };

            _imageDb.Images.Add(image);
            return _imageDb.SaveChanges() > 0;
        }
    }
}
