using ELKH.Data;
using ELKH.Models;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Repositories
{
    public class InventoryRepo : IInventoryRepo
    {
        private readonly ApplicationDbContext _context;
        private readonly ImageStoreContext _imageDb;
        private readonly RoleManager<IdentityRole> _roleManager;

        public InventoryRepo(ApplicationDbContext context, ImageStoreContext imageDb, RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _imageDb = imageDb;
            _roleManager = roleManager;
        }

        // ─── GetAllProduct (with sort) ────────────────────────────────────────
        public async Task<PagedResult<InventoryVM>> GetAllProduct(
            string? searchString,
            string? sortOrder,
            string? stockFilter,
            int page = 1,
            int pageSize = 10)
        {
            var query = _context.Product.AsQueryable();

            // 1. Filter by Search
            if (!string.IsNullOrEmpty(searchString))
            {
                var s = searchString.ToLower();
                query = query.Where(p => p.Name.ToLower().Contains(s) ||
                                         p.Description.ToLower().Contains(s));
            }

            // 2. Filter by Stock (matching your Manager logic)
            if (stockFilter == "low")
            {
                query = query.Where(p => p.StockQuantity <= 20);
            }
            else if (stockFilter == "stocked")
            {
                query = query.Where(p => p.StockQuantity >= 21);
            }

            // FIX: added price_asc / price_desc cases to match the sort buttons
            query = sortOrder switch
            {
                "name_desc" => query.OrderByDescending(p => p.Name),
                "price_asc" => query.OrderBy(p => p.Price),
                "price_desc" => query.OrderByDescending(p => p.Price),
                _ => query.OrderBy(p => p.Name)   // default: A-Z
            };

            var totalItems = await query.CountAsync();

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                // FIX: Price now included in the projection
                .Select(p => new InventoryVM
                {
                    PkProductId = p.PkProductId,
                    ProductName = p.Name,
                    Quantity = p.StockQuantity ?? 0,
                    IsActive = p.IsActive,
                    Price = p.Price
                })
                .ToListAsync();

            return new PagedResult<InventoryVM>
            {
                Items = items,
                PageSize = pageSize,
                CurrentPage = page,
                TotalItems = totalItems
            };
        }

        // ─── GetAllProduct (search-only overload, kept for any other callers) ─
        public async Task<PagedResult<InventoryVM>> GetAllProduct(
            string? searchString,
            int page = 1,
            int pageSize = 10)
        {
            var query = _context.Product.AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                var s = searchString.ToLower();
                query = query.Where(p =>
                    p.Name.ToLower().Contains(s) ||
                    p.Description.ToLower().Contains(s));
            }

            var total = await query.CountAsync();

            var items = await query
                .OrderBy(p => p.PkProductId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new InventoryVM
                {
                    PkProductId = p.PkProductId,
                    ProductName = p.Name,
                    Quantity = p.StockQuantity ?? 0,
                    IsActive = p.IsActive,
                    Price = p.Price
                })
                .ToListAsync();

            return new PagedResult<InventoryVM>
            {
                Items = items,
                PageSize = pageSize,
                CurrentPage = page,
                TotalItems = total
            };
        }

        // ─── GetProductById ───────────────────────────────────────────────────
        public async Task<ProductModel> GetProductById(int Id)
        {
            return await _context.Product
                .Include(p => p.Category)
                .Include(p => p.ProductRatings!)
                    .ThenInclude(pr => pr.RegisteredUser)
                .FirstOrDefaultAsync(p => p.PkProductId == Id) ?? null!;
        }

        // ─── EditProduct ──────────────────────────────────────────────────────
        public async Task<bool> EditProduct(ProductVM vm)
        {
            var product = await GetProductById(vm.ProductId);
            if (product == null) return false;

            product.Name = vm.ProductName;
            product.Description = vm.Description;
            product.Price = vm.Price;
            product.FkCategoryId = vm.CategoryId;
            product.StockQuantity = vm.StockQuantity;
            product.DiscountPercent = vm.DiscountPercent;
            product.IsActive = vm.IsActive;

            await _context.SaveChangesAsync();
            return true;
        }

        // ─── AddProduct ───────────────────────────────────────────────────────
        public async Task<int> AddProduct(ProductVM vm)
        {
            var product = new ProductModel
            {
                Name = vm.ProductName,
                NameNormalized = vm.ProductName.ToLower(),
                Price = vm.Price,
                DiscountPercent = vm.DiscountPercent,
                StockQuantity = vm.StockQuantity,
                IsActive = vm.IsActive,
                FkCategoryId = vm.CategoryId,
                Description = vm.Description
            };

            await _context.Product.AddAsync(product);
            await _context.SaveChangesAsync();
            return product.PkProductId;
        }

        // ─── EditProductQuantity ──────────────────────────────────────────────
        public async Task<ProductVM> EditProductQuantity(int productId, int quantityAmount)
        {
            var product = await _context.Product
                .Where(p => p.PkProductId == productId)
                .FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException($"Product {productId} not found.");

            product.StockQuantity = quantityAmount;
            await _context.SaveChangesAsync();

            return new ProductVM
            {
                ProductId = product.PkProductId,
                ProductName = product.Name,
                Description = product.Description,
                Price = product.Price,
                StockQuantity = product.StockQuantity,
                IsActive = product.IsActive
            };
        }

        // ─── DeleteProductReview ──────────────────────────────────────────────
        public async Task<bool> DeleteProductReview(int reviewId)
        {
            var review = await _context.ProductRatings
                .FirstOrDefaultAsync(pr => pr.PkRatingId == reviewId);
            if (review == null) return false;

            _context.ProductRatings.Remove(review);
            await _context.SaveChangesAsync();
            return true;
        }

        // ─── GetAllCategories ─────────────────────────────────────────────────
        public async Task<List<CategoryModel>> GetAllCategories()
        {
            return await _context.Categories.ToListAsync();
        }

        // ─── GetProductImages ─────────────────────────────────────────────────
        public async Task<List<ImageModel>> GetProductImages(int id)
        {
            return await _imageDb.Images
                .Where(pi => pi.FkProductId == id)
                .ToListAsync();
        }

        // ─── UploadImage ──────────────────────────────────────────────────────
        private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

        public async Task<bool> UploadImage(int productId, IFormFile file)
        {
            if (file == null || file.Length == 0 || file.Length > MaxFileSizeBytes)
                return false;

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            var bytes = stream.ToArray();

            var image = new ImageModel
            {
                FileName = file.FileName,
                Description = string.Empty,
                FileType = file.ContentType,
                ImageData = bytes,
                FkProductId = productId
            };

            _imageDb.Images.Add(image);
            return _imageDb.SaveChanges() > 0;
        }

        // ─── DeleteImage ──────────────────────────────────────────────────────
        public async Task<bool> DeleteImage(int imageId)
        {
            var image = await _imageDb.Images
                .FirstOrDefaultAsync(i => i.ImageId == imageId);
            if (image == null) return false;

            _imageDb.Images.Remove(image);
            _imageDb.SaveChanges();
            return true;
        }
    }
}