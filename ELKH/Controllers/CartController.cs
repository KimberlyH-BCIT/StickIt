using Microsoft.AspNetCore.Mvc;
using ELKH.Models;
using System.Collections.Generic;
using System.Linq;

namespace ELKH.Controllers;

public class CartController : Controller
{
    private static List<ProductModel> _cartItems = new();

    public IActionResult Index()
    {
        return View(_cartItems);
    }

    [HttpPost]
    public IActionResult AddToCart(int itemId, int quantity)
    {
        if (quantity <= 0) return BadRequest("Quantity must be positive.");

        // Fake product for now, will be replaced later
        _cartItems.Add(new ProductModel { Name = $"Product {itemId}", Price = 10.00m });

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult RemoveFromCart(int itemId)
    {
        // Find product by id in cart
        var item = _cartItems.FirstOrDefault(p => p.PkProductId == itemId);
        if (item != null) _cartItems.Remove(item);

        return RedirectToAction(nameof(Index));
    }
}