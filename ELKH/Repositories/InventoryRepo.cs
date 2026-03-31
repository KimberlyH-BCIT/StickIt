using ELKH.Data;
using ELKH.Models;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
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
        public async Task<PagedResult<InventoryVM>> GetAllProduct(string? searchString, int page = 1, int pageSize = 10)
        {
            // 1. Start with the base query
            var query = _context.Products.AsQueryable();

            // 2. Apply filtering (build on 'query')
            if (!string.IsNullOrEmpty(searchString))
            {
                var search = searchString.ToLower();
                query = query.Where(p => p.Name.ToLower().Contains(search)
                                      || p.Description.ToLower().Contains(search));
            }

            // 3. Count the filtered results
            var countProducts = await query.CountAsync();

            // 4. Order, Paginate, and PROJECT directly to VM
            var items = await query
                .OrderBy(p => p.PkProductId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new InventoryVM // Mapping here saves database bandwidth
                {
                    PkProductId = p.PkProductId,
                    ProductName = p.Name,
                    Quantity = p.StockQuantity,
                    IsActive = p.IsActive,
                })
                .ToListAsync();

            // 5. Return the result
            return new PagedResult<InventoryVM>
            {
                Items = items,
                PageSize = pageSize,
                CurrentPage = page,
                TotalItems = countProducts
            };
        }

        public async Task<bool> EditProduct(ProductVM vm)
        {
            var product = await GetProductById(vm.ProductId);

            if(product == null)
            {
                return false;
            }

            product.Name = vm.ProductName;
            product.Description = vm.Description;
            product.Price = vm.Price;
            product.FkCategoryId = vm.CategoryId;
            product.StockQuantity = vm.StockQuantity;
            product.DiscountPercent = vm.DiscountPercent;

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<ProductModel> GetProductById (int Id)
        {

            var productModel = await _context.Products.Include(p=> p.Category)
                                                      .Include(p=> p.ProductRatings)
                                                      .ThenInclude(pr => pr.RegisteredUser)
                                                      .FirstOrDefaultAsync(p => p.PkProductId == Id);

            return productModel;
        }

        public async Task<bool> DeleteProductReview(int reviewId)
        {
            var review = await _context.ProductRatings.FirstOrDefaultAsync(pr => pr.PkRatingId == reviewId);
            if(review == null)
            {
                return false;
            }

            _context.ProductRatings.Remove(review);
            await _context.SaveChangesAsync();
            return true;
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

        public async Task<int> AddProduct(ProductVM vm)
        {

            var mapToProductModel = new ProductModel
            {
                Name = vm.ProductName,
                NameNormalized = vm.Description,
                Price = vm.Price,
                DiscountPercent = vm.DiscountPercent,
                StockQuantity = vm.StockQuantity,
                IsActive = vm.IsActive,
                FkCategoryId = vm.CategoryId
            };

            await _context.Products.AddAsync(mapToProductModel);
            await _context.SaveChangesAsync();



            return mapToProductModel.PkProductId;

        }

        public async Task<List<CategoryModel>> GetAllCategories()
        {

            var categories = await _context.Categories.ToListAsync();

            return categories;
            
        }

    }
} 

