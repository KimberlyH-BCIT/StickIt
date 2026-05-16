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
            if (quantity > 0) return true;

            modelState.AddModelError(fieldName, "Quantity must be greater than zero.");
            return false;
        }

        /// <summary>
        /// Validate that a product is in stock and has sufficient quantity.
        /// </summary>
        public static bool IsInStock(this ProductModel product, int requestedQuantity, ModelStateDictionary modelState)
        {
            return product switch
            {
                null => AddErrorAndReturnFalse(modelState, "Product not found."),
                { IsActive: false } => AddErrorAndReturnFalse(modelState, "This product is no longer available."),
                { StockQuantity: var stock } when stock < requestedQuantity =>
                    AddErrorAndReturnFalse(modelState, $"Only {stock} items available in stock."),
                _ => true
            };
        }

        private static bool AddErrorAndReturnFalse(ModelStateDictionary modelState, string errorMessage)
        {
            modelState.AddModelError(string.Empty, errorMessage);
            return false;
        }

        /// <summary>
        /// Calculate the effective price after discount.
        /// </summary>
        public static decimal GetEffectivePrice(this ProductModel product)
        {
            if (product.DiscountPercent > 0)
            {
                return decimal.Round(product.Price * (1 - (product.DiscountPercent / 100m)), 2, MidpointRounding.AwayFromZero);
            }
            return decimal.Round(product.Price, 2, MidpointRounding.AwayFromZero);
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
