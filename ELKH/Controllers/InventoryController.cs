using System.Linq;
using System.Threading.Tasks;
using ELKH.Models;
using ELKH.Repositories;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ELKH.Controllers
{
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

        [HttpPost]
        public async Task<IActionResult> EditProductAmount(int productId, int quantityId)
        {
            await _inventoryRepo.EditProductQuantity(productId, quantityId);

            return RedirectToAction(nameof(Index));
        }

        // Keep a single ProductImages action (GET) that accepts an int productId
        public async Task<IActionResult> ProductImages(int productId)
        {
            ViewBag.ProductId = productId;
            var productImages = await _inventoryRepo.GetProductImages(productId);

            if (productImages == null)
            {
                return NotFound();
            }

            // Avoid accessing properties on 'pi' that may not exist (e.g. if pi is a string).
            // Use the known productId for FkProductId. ProductImage is set to null because
            // an IFormFile is not available when reading images from the repository.
            var vmList = productImages.Select(pi => new ProductImageVM
            {
                ProductImage = null,
                FkProductId = productId
            }).ToList();

            return View(vmList);
        }

        // GET: show add-image form for a specific product
        public async Task<IActionResult> AddImage(int productId)
        {
            var vm = new ImageModel();
            ViewBag.ProductId = productId;

            return View(vm);
        }

        // POST: add image using a view model
        [HttpPost]
        public async Task<IActionResult> AddImage(int productId, IFormFile file)
        {
            if (!ModelState.IsValid)
            {
                return View("AddImage");
            }

            var addImageRepo = await _inventoryRepo.UploadImage(productId, file);

            if (addImageRepo)
            {
                return RedirectToAction("ProductImages", new { id = productId });
            }
            return View("Index");
        }

      
    }
}
