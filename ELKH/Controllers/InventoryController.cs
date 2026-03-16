using System.Linq;
using ELKH.Models;
using ELKH.Repositories;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SQLitePCL;

namespace ELKH.Controllers
{
    /// <summary>
    /// Admin controller for inventory management: listing products, adjusting stock
    /// quantities, and managing product images.
    /// </summary>
    
    [Authorize(Roles = "Admin, Staff")]
    public class InventoryController : Controller
    {
        private readonly InventoryRepo _inventoryRepo;
        
        
        public InventoryController(InventoryRepo inventoryRepo)
        {
            _inventoryRepo = inventoryRepo;
        }


        public async Task<IActionResult> Index()
        {
            var products = await _inventoryRepo.GetAllProduct();

            var inventoryList = products.Select(p => new InventoryVM
            {
                PkProductId = p.PkProductId,
                ProductName = p.Name,
                Quantity = p.StockQuantity,
                IsActive = p.IsActive
            }).ToList();

            return View(inventoryList);
        }

        public async Task<IActionResult> EditProduct(int Id)
        {
            //Fetch product by Id
            var getProduct = await _inventoryRepo.GetProductById(Id);
            var categories = await _inventoryRepo.GetAllCategories();



            //Convert Entity to VM
            var mapToVM = new ProductVM
            {
                ProductId = Id,
                ProductName = getProduct.Name,
                Description = getProduct.Description,
                Price = getProduct.Price,
                DiscountPercent = getProduct.DiscountPercent,
                StockQuantity = getProduct.StockQuantity,
                IsActive = getProduct.IsActive,
                CategoryId = getProduct.FkCategoryId,
                ProductReviews = getProduct.ProductRatings?.Select(pr => new ReviewDisplayVM
                {
                    RatingId = pr.PkRatingId,
                    Rating = pr.Rating,
                    Description = pr.Description,
                    CreatedAt = pr.RatedTime,
                    LastEditedAt = pr.LastEditedAt,
                    ReviewerFirstName = pr.RegisteredUser.Email
                }).ToList()
            };

            ViewBag.Categories = new SelectList(categories, "PkCategoryId", "CategoryName");
            ViewBag.Id = Id;
            return View(mapToVM);
        }

        [HttpPost]
        public async Task<IActionResult> EditProduct(ProductVM vm)
        {
            if (!ModelState.IsValid)
            {
                var categories = await _inventoryRepo.GetAllCategories();
                ViewBag.Categories = new SelectList(categories, "PkCategoryId", "CategoryName");

                return RedirectToAction("EditProduct", new { Id = vm.ProductId });
            }

            await _inventoryRepo.EditProduct(vm);

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteProductReview(int reviewId, int productId)
        {

            await _inventoryRepo.DeleteProductReview(reviewId);

            return RedirectToAction("EditProduct", new { Id = productId });
        }

        public async Task<IActionResult> AddProduct()
        {
            // Initialize a non-null view model so tag helpers that read Model values
            // (for example SelectTagHelper when using asp-for) do not throw.
            var vm = new ProductVM();
            var categories = await _inventoryRepo.GetAllCategories();
            ViewBag.Categories = new SelectList(categories, "PkCategoryId", "CategoryName");
            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> AddProduct(ProductVM vm)
        {
            if (!ModelState.IsValid)
            {
                var categories = await _inventoryRepo.GetAllCategories();
                ViewBag.Categories = new SelectList(categories, "CategoryId", "Category");

                return View(vm);
            }

            var saveToDataBase = await _inventoryRepo.AddProduct(vm);

            return RedirectToAction("AddImage", new { productId = saveToDataBase});
        }

        [HttpPost]
        public async Task<IActionResult> EditProductAmount(int productId, int quantityId)
        {
            await _inventoryRepo.EditProductQuantity(productId, quantityId);

            return RedirectToAction(nameof(Index));
        }



        //Pass
        public async Task<IActionResult> ProductImages(int Id)
        {

            ViewBag.ProductId = Id;
            // GetProductImages likely returns List<ImageModel>
            var productImages = await _inventoryRepo.GetProductImages(Id);

            if (productImages == null)
            {
                return NotFound();
            }

            // Avoid accessing properties on 'pi' that may not exist (e.g. if pi is a string).
            // Use the known productId for FkProductId. ProductImage is set to null because
            // an IFormFile is not available when reading images from the repository.
            var vmList = productImages.Select(pi => new ProductImageVM
            {
                ImageId = pi.ImageId,
                ImageData = pi.ImageData,
                FkProductId = Id
            }).ToList();

            return View(vmList);
        }

        // GET: show add-image form for a specific product
        public async Task<IActionResult> AddImage(int Id)
        {
            ViewBag.ProductId = Id;

            return View();
        }


        //Pass
        [HttpPost]
        public async Task<IActionResult> AddImage(int productId, IFormFile file)
        {
            if (!ModelState.IsValid)
            {
                return RedirectToAction("AddImage", new { Id = productId });
            }

            var addImageRepo = await _inventoryRepo.UploadImage(productId, file);

            if (addImageRepo)
            {
                return RedirectToAction("ProductImages", new { id = productId });
            }
            return View("Index");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteImage(int productId ,int imageId)
        {
            var deleteImageRepo = await _inventoryRepo.DeleteImage(imageId);
            if (deleteImageRepo)
            {
                return RedirectToAction("ProductImages", new { id = productId });
            }
            return View("Index");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteProductReview(int reviewId)
        {
            return View();
        }
    }
}
