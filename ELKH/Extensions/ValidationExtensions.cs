using ELKH.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ELKH.Extensions
{
    /// <summary>
    /// Extension methods for model validation and business rule enforcement.
    /// Centralizes common validation patterns used across the application.
    /// </summary>
    public static class ValidationExtensions
    {
        /// <summary>
        /// Validate that a quantity is positive.
        /// </summary>
        public static bool IsValidQuantity(this int quantity, ModelStateDictionary modelState, string fieldName = "Quantity")
        {
            if (quantity <= 0)
            {
                modelState.AddModelError(fieldName, "Quantity must be greater than zero.");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Validate that a product is in stock and has sufficient quantity.
        /// </summary>
        public static bool IsInStock(this ProductModel product, int requestedQuantity, ModelStateDictionary modelState)
        {
            if (product == null)
            {
                modelState.AddModelError(string.Empty, "Product not found.");
                return false;
            }

            if (!product.IsActive)
            {
                modelState.AddModelError(string.Empty, "This product is no longer available.");
                return false;
            }

            if (product.StockQuantity < requestedQuantity)
            {
                modelState.AddModelError(string.Empty, 
                    $"Only {product.StockQuantity} items available in stock.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Calculate the effective price after discount.
        /// </summary>
        public static decimal GetEffectivePrice(this ProductModel product)
        {
            if (product.DiscountPercent > 0)
            {
                return product.Price * (1 - (product.DiscountPercent / 100m));
            }
            return product.Price;
        }

        /// <summary>
        /// Check if a product has an active discount.
        /// </summary>
        public static bool HasDiscount(this ProductModel product)
        {
            return product.DiscountPercent > 0;
        }
    }
}
