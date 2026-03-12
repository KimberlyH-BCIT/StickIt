using System.Linq;
using ELKH.Models;
using ELKH.Repositories;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ELKH.Controllers
{
    /// <summary>
    /// Admin controller for inventory management: listing products, adjusting stock
    /// quantities, and managing product images.
    /// </summary>
[Authorize(Roles = "Admin")]
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
            }).ToList();

            return View(inventoryList);
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
        [RequestSizeLimit(5 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 5 * 1024 * 1024)]
        public async Task<IActionResult> AddImage(int productId, IFormFile file)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.ProductId = productId;
                return View("AddImage");
            }

            if (file == null || file.Length == 0)
            {
                ModelState.AddModelError("", "Please select a file.");
                ViewBag.ProductId = productId;
                return View("AddImage");
            }

            var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" };
            var ext = Path.GetExtension(file.FileName);
            if (!allowedExtensions.Contains(ext))
            {
                ModelState.AddModelError("", "Only image files are allowed (jpg, jpeg, png, gif, webp, bmp).");
                ViewBag.ProductId = productId;
                return View("AddImage");
            }

            var allowedMimeTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "image/jpeg", "image/png", "image/gif", "image/webp", "image/bmp" };
            if (!allowedMimeTypes.Contains(file.ContentType))
            {
                ModelState.AddModelError("", "The uploaded file has an invalid content type.");
                ViewBag.ProductId = productId;
                return View("AddImage");
            }

            var addImageRepo = await _inventoryRepo.UploadImage(productId, file);

            if (addImageRepo)
            {
                return RedirectToAction("ProductImages", new { id = productId });
            }

            ModelState.AddModelError("", "The file could not be saved. Ensure it is a valid image under 5 MB.");
            ViewBag.ProductId = productId;
            return View("AddImage");
        }

      
    }
}
