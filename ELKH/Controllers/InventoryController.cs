using System.Linq;
using ELKH.Models;
using ELKH.Repositories;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ELKH.Controllers
{
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
                Quantity = p.StockQuantity ?? 0,
                IsActive = p.IsActive
            }).ToList();

            return View(inventoryList);
        }

        [HttpPost]
        public async Task<IActionResult> EditProductAmount(int productId, int quantity)
        {
            await _inventoryRepo.EditProductQuantity(productId, quantity);
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> ProductImages(int productId)
        {
            ViewBag.ProductId = productId;

            var productImages = await _inventoryRepo.GetProductImages(productId);

            if (productImages == null)
            {
                return NotFound();
            }

            var vmList = productImages.Select(pi => new ProductImageVM
            {
                ProductImage = null,
                FkProductId = productId
            }).ToList();

            return View(vmList);
        }

        public IActionResult AddImage(int productId)
        {
            ViewBag.ProductId = productId;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> AddImage(int productId, IFormFile file)
        {
            if (!ModelState.IsValid)
            {
                return View("AddImage");
            }

            var success = await _inventoryRepo.UploadImage(productId, file);

            if (success)
            {
                return RedirectToAction("ProductImages", new { productId = productId });
            }

            return View("Index");
        }
    }
}