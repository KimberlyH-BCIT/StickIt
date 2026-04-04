using System.Linq;
using ELKH.Models;
using ELKH.Repositories;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ELKH.Controllers
{
    [Authorize(Roles = "Admin, Staff")]
    public class InventoryController : Controller
    {
        private readonly InventoryRepo _inventoryRepo;

        public InventoryController(InventoryRepo inventoryRepo)
        {
            _inventoryRepo = inventoryRepo;
        }

        private async Task<List<ProductImageVM>> SafeGetImages(int productId)
        {
            try
            {
                var rows = await _inventoryRepo.GetProductImages(productId);
                if (rows == null) return new List<ProductImageVM>();

                return rows.Select(pi => new ProductImageVM
                {
                    ImageId = pi.ImageId,
                    ImageData = pi.ImageData,
                    FkProductId = productId,
                    FileName = pi.FileName ?? string.Empty,
                    Description = pi.Description ?? string.Empty
                }).ToList();
            }
            catch
            {
                TempData["ImageDbError"] =
                    "⚠️ Image database is not set up yet. Run 'dotnet ef database update'.";
                return new List<ProductImageVM>();
            }
        }

        public async Task<IActionResult> Index(string? searchString, string? sortOrder, string? stockFilter, int page = 1)
        {
            int pageSize = 10;

            var products = await _inventoryRepo.GetAllProduct(searchString, sortOrder, stockFilter, page, pageSize);

            ViewBag.CurrentFilter = searchString;
            ViewBag.CurrentSort = sortOrder;
            ViewBag.StockFilter = stockFilter;

            return View(products);
        }

        public async Task<IActionResult> Detail(int Id)
        {
            var product = await _inventoryRepo.GetProductById(Id);
            if (product == null) return NotFound();

            var vm = new ProductVM
            {
                ProductId = Id,
                ProductName = product.Name,
                Description = product.Description,
                Price = product.Price,
                DiscountPercent = product.DiscountPercent,
                StockQuantity = product.StockQuantity,
                IsActive = product.IsActive,
                CategoryId = product.FkCategoryId,
                CategoryName = product.Category?.CategoryName ?? string.Empty,
                ExistingImages = await SafeGetImages(Id)
            };

            return View(vm);
        }

        public async Task<IActionResult> EditProduct(int Id)
        {
            var product = await _inventoryRepo.GetProductById(Id);
            if (product == null) return NotFound();

            var categories = await _inventoryRepo.GetAllCategories();
            ViewBag.Categories = new SelectList(categories, "PkCategoryId", "CategoryName");

            var vm = new ProductVM
            {
                ProductId = Id,
                ProductName = product.Name,
                Description = product.Description,
                Price = product.Price,
                DiscountPercent = product.DiscountPercent,
                StockQuantity = product.StockQuantity,
                IsActive = product.IsActive,
                CategoryId = product.FkCategoryId,
                ExistingImages = await SafeGetImages(Id),
                ProductReviews = product.ProductRatings?.Select(pr => new ReviewDisplayVM
                {
                    RatingId = pr.PkRatingId,
                    Rating = pr.Rating,
                    Description = pr.Description,
                    CreatedAt = pr.RatedTime,
                    LastEditedAt = pr.LastEditedAt,
                    ReviewerFirstName = pr.RegisteredUser.Email
                }).ToList()
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProduct(ProductVM vm)
        {
            if (!ModelState.IsValid)
            {
                var categories = await _inventoryRepo.GetAllCategories();
                ViewBag.Categories = new SelectList(categories, "PkCategoryId", "CategoryName");
                vm.ExistingImages = await SafeGetImages(vm.ProductId);
                return View(vm);
            }

            if (vm.NewImages != null && vm.NewImages.Count > 0)
            {
                var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" };

                foreach (var file in vm.NewImages)
                {
                    if (file == null || file.Length == 0) continue;
                    if (!allowed.Contains(Path.GetExtension(file.FileName))) continue;
                    await _inventoryRepo.UploadImage(vm.ProductId, file);
                }
            }

            TempData["Success"] = "Product updated successfully.";
            return RedirectToAction("EditProduct", new { Id = vm.ProductId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProductReview(int reviewId, int productId)
        {
            await _inventoryRepo.DeleteProductReview(reviewId);
            return RedirectToAction("EditProduct", new { Id = productId });
        }

        public async Task<IActionResult> AddProduct()
        {
            var categories = await _inventoryRepo.GetAllCategories();
            ViewBag.Categories = new SelectList(categories, "PkCategoryId", "CategoryName");
            return View(new ProductVM());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(5 * 1024 * 1024)]
        public async Task<IActionResult> AddProduct(ProductVM vm)
        {
            if (!ModelState.IsValid)
            {
                var categories = await _inventoryRepo.GetAllCategories();
                ViewBag.Categories = new SelectList(categories, "PkCategoryId", "CategoryName");
                return View(vm);
            }

            var savedId = await _inventoryRepo.AddProduct(vm);

            if (vm.NewImages != null && vm.NewImages.Count > 0)
            {
                var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" };

                foreach (var file in vm.NewImages)
                {
                    if (file == null || file.Length == 0) continue;
                    if (!allowed.Contains(Path.GetExtension(file.FileName))) continue;
                    await _inventoryRepo.UploadImage(savedId, file);
                }
            }

            TempData["Success"] = "Product added successfully.";
            return RedirectToAction("Detail", new { Id = savedId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProductAmount(int productId, int quantityId)
        {
            await _inventoryRepo.EditProductQuantity(productId, quantityId);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteImage(int productId, int imageId)
        {
            await _inventoryRepo.DeleteImage(imageId);
            return RedirectToAction("EditProduct", new { Id = productId });
        }

        public async Task<IActionResult> ProductImages(int Id)
        {
            ViewBag.ProductId = Id;
            var images = await SafeGetImages(Id);
            return View(images);
        }

        public IActionResult AddImage(int productId)
        {
            ViewBag.ProductId = productId;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddImage(int productId, IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                ModelState.AddModelError("", "Please select a file.");
                ViewBag.ProductId = productId;
                return View();
            }

            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" };

            if (!allowed.Contains(Path.GetExtension(file.FileName)))
            {
                ModelState.AddModelError("", "Only image files are allowed.");
                ViewBag.ProductId = productId;
                return View();
            }

            await _inventoryRepo.UploadImage(productId, file);
            return RedirectToAction("EditProduct", new { Id = productId });
        }
    }
}
