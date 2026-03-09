using System.Linq;
using ELKH.Models;
using ELKH.Repositories;
using ELKH.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace ELKH.Controllers
{
    /// <summary>
    /// Admin controller for inventory management: listing products, adjusting stock
    /// quantities, and managing product images.
    /// </summary>
    //[Authorize(Roles = "Admin")]
    public class InventoryController : Controller
    {
        private readonly InventoryRepo _inventoryRepo;
        
        public InventoryController(InventoryRepo inventoryRepo)
        {
            _inventoryRepo = inventoryRepo;
        }


        //Pass
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

        //Pass
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

            // Map each ImageModel to ProductImageVM
            var vmList = productImages.Select(pi => new ProductImageVM
            {
                FileName = pi.FileName,
                Description = pi.Description,
                ImageData = pi.ImageData
            }).ToList();

            return View(vmList);
        }

        //Pass
        public async Task<IActionResult> AddImage(int Id)
        {
            var vm = new ImageModel();
            ViewBag.ProductId = Id;

            return View(vm);
        }


        //Pass
        [HttpPost]
        public async Task<IActionResult> AddImage(int productId, IFormFile file)
        {
            if (!ModelState.IsValid)
            {
                return View("AddImage");
            }

            var addImageRepo = await _inventoryRepo.UploadImage(productId,file);

            if (addImageRepo)
            {
                return RedirectToAction("ProductImages", new {id = productId});
            }
            return View("Index");
        }
    }
}
