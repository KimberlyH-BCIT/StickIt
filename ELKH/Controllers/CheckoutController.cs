using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

namespace ELKH.Controllers;

public class CheckoutController:Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        
        return View();
    }

        
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ProcessPayment(string paymentToken)
    {
        if (string.IsNullOrEmpty(paymentToken))
        {
            ModelState.AddModelError("", "Payment token is required.");
            return View("Index");
        }

            
        bool paymentSuccess = true; 

        if (paymentSuccess)
        {
            return RedirectToAction(nameof(Complete));
        }
        else
        {
            ModelState.AddModelError("", "Payment failed.");
            return View("Index");
        }
    }

        
    public IActionResult Complete()
    {
            
        ViewBag.OrderId = "ORDER-12345-MOCK";
        return View();
    }
}