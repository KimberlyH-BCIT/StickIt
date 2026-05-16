using System.Linq;
using ELKH.Models;
using ELKH.Repositories;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Controllers
{
    /// <summary>
    /// Inventory management controller accessible to Admin, Manager, and Staff roles:
    /// listing products, adjusting stock quantities, and managing product images.
    /// </summary>
    /// <remarks>
/// <para><strong>Table of Contents:</strong></para>
/// <list type="number">
/// <item>Section 1: Controller Setup &amp; Dependencies</item>
/// <item>Section 2: Inventory Display &amp; Listing</item>
/// <item>Section 3: Stock Quantity Management</item>
/// <item>Section 4: Product Image Management</item>
/// </list>
/// 
    /// <para><strong>Multi-Role Administrative Access</strong></para>
    /// This controller requires Admin, Manager, or Staff role authorization for all operations.
    /// 
    /// <para><strong>Core Features:</strong></para>
    /// <list type="bullet">
    /// <item>Product inventory listing with current stock levels</item>
    /// <item>Stock quantity adjustments with validation</item>
    /// <item>Automated customer notification for stock replenishment</item>
    /// <item>Product image management with security validation</item>
    /// </list>
    /// 
    /// <para><strong>Notification System:</strong></para>
    /// When products are restocked from out-of-stock status, the system automatically
    /// processes customer notifications with a 24-hour cooldown period to prevent spam.
    /// </remarks>
    [Authorize(Roles = "Admin,Manager,Staff")]
    public class InventoryController : Controller
    {
        #region Section 1: Controller Setup & Dependencies

        // ===================================================================
        // Section 1: Controller Setup & Dependencies
        // ===================================================================

        private readonly IInventoryRepo _inventoryRepo;
        private readonly ELKH.Services.ImageValidationService _imageValidator;
        private readonly ELKH.Services.StockNotificationEmailService _stockNotificationService;
        private readonly ELKH.Data.ApplicationDbContext _db;

        public InventoryController(
            IInventoryRepo inventoryRepo,
            ELKH.Services.ImageValidationService imageValidator,
            ELKH.Services.StockNotificationEmailService stockNotificationService,
            ELKH.Data.ApplicationDbContext db)
        {
            _inventoryRepo = inventoryRepo;
            _imageValidator = imageValidator;
            _stockNotificationService = stockNotificationService;
            _db = db;
        }

        #endregion

        #region Section 2: Inventory Display & Listing

        // ===================================================================
        // Section 2: Inventory Display & Listing
        // ===================================================================


        public async Task<IActionResult> Index()
        {
            var result = await _inventoryRepo.GetAllProduct(null);

            return View(result.Items);
        }

        #endregion

        #region Section 3: Stock Quantity Management

        // ===================================================================
        // Section 3: Stock Quantity Management
        // ===================================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProductAmount(int productId, int quantityId)
        {
            // Check current stock before update
            var product = await _db.Products.FindAsync(productId);
            if (product == null)
            {
                TempData["Message"] = "error, Product not found.";
                return RedirectToAction(nameof(Index));
            }

            var wasOutOfStock = product.StockQuantity == null || product.StockQuantity == 0;

            // Update the stock quantity
            await _inventoryRepo.EditProductQuantity(productId, quantityId);

            // Only trigger notifications if:
            // 1. Product was previously out of stock
            // 2. Now has positive stock
            // 3. No notifications sent in the last 24 hours (checked in service)
            if (wasOutOfStock && quantityId > 0)
            {
                // Check if there are any pending notifications before triggering
                var hasPendingNotifications = await _db.StockNotifications
                    .AnyAsync(sn => sn.FkProductId == productId
                                 && !sn.NotificationSent
                                 && !sn.IsCancelled);

                if (hasPendingNotifications)
                {
                    // Fire and forget - don't block the response
                    // The service will check cooldown internally
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            // Use default 24-hour cooldown
                            await _stockNotificationService.ProcessRestockNotificationsAsync(productId, cooldownHours: 24);
                        }
                        catch (Exception ex)
                        {
                            // Log error but don't fail the request
                            Console.WriteLine($"Error sending stock notifications: {ex.Message}");
                        }
                    });

                    TempData["Message"] = "success, Stock updated! Notification emails are being sent to waiting customers.";
                }
                else
                {
                    TempData["Message"] = "success, Stock updated successfully.";
                }
            }
            else if (quantityId > 0)
            {
                TempData["Message"] = "success, Stock updated successfully.";
            }
            else
            {
                TempData["Message"] = "success, Stock set to zero (out of stock).";
            }

            return RedirectToAction(nameof(Index));
        }

        #endregion

        #region Section 4: Product Image Management

        // ===================================================================
        // Section 4: Product Image Management
        // ===================================================================

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

            var vmList = productImages.Select(pi => new ProductImageVM
            {
                ImageId = pi.ImageId,
                FileName = pi.FileName,
                Description = pi.Description,
                ImageData = pi.ImageData
            }).ToList();

            return View(vmList);
        }

        public async Task<IActionResult> AddImage(int productId)
        {
            var vm = new ImageModel();
            ViewBag.ProductId = productId;

            return View(vm);
        }


        //Pass
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(5 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 5 * 1024 * 1024)]
        public async Task<IActionResult> AddImage(int productId, IFormFile file)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.ProductId = productId;
                return View("AddImage");
            }

            // ===============================================================
            // SECURITY: Multi-layer image validation
            // Validates file through:
            // - Magic byte verification (prevents disguised executables)
            // - Extension and MIME type whitelisting
            // - Image dimension limits (prevents memory exhaustion)
            // - File size limits (enforced at middleware level + validation)
            // - Filename sanitization (prevents path traversal)
            // ===============================================================
            var validationResult = await _imageValidator.ValidateImageAsync(file);

            if (!validationResult.IsValid)
            {
                foreach (var error in validationResult.Errors)
                {
                    ModelState.AddModelError("", error);
                }
                ViewBag.ProductId = productId;
                return View("AddImage");
            }

            // Upload validated image with sanitized filename
            var addImageRepo = await _inventoryRepo.UploadImage(productId, file);

            if (addImageRepo)
            {
                TempData["Message"] = $"success, Image uploaded successfully ({validationResult.ImageWidth}x{validationResult.ImageHeight})";
                return RedirectToAction("ProductImages", new { id = productId });
            }

            ModelState.AddModelError("", "The file could not be saved. Please try again.");
            ViewBag.ProductId = productId;
            return View("AddImage");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteImage(int productId, int imageId)
        {
            await _inventoryRepo.DeleteImage(imageId);
            TempData["Message"] = "success, Image deleted successfully.";
            return RedirectToAction("ProductImages", new { id = productId });
        }

        #endregion

    }
}
