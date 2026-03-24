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
    /// Admin controller for inventory management: listing products, adjusting stock
    /// quantities, and managing product images.
    /// </summary>
[Authorize(Roles = "Admin")]
public class InventoryController : Controller
{
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


public async Task<IActionResult> Index()
        {
            var products = await _inventoryRepo.GetAllProduct();

            var inventoryList = products.Select(p => new InventoryVM
            {
                PkProductId = p.PkProductId,
                ProductName = p.Name,
                Quantity = p.StockQuantity,
            }).ToList();

            return View(inventoryList);
        }

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

            // ═══════════════════════════════════════════════════════════════
            // SECURITY: Multi-layer image validation
            // Validates file through:
            // - Magic byte verification (prevents disguised executables)
            // - Extension and MIME type whitelisting
            // - Image dimension limits (prevents memory exhaustion)
            // - File size limits (enforced at middleware level + validation)
            // - Filename sanitization (prevents path traversal)
            // ═══════════════════════════════════════════════════════════════
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

      
    }
}
