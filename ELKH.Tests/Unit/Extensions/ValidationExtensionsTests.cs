using ELKH.Extensions;
using ELKH.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xunit;

namespace ELKH.Tests.Unit.Extensions;

/// <summary>
/// Unit tests for validation extension helpers.
/// </summary>
/// <remarks>
/// 1. IsValidQuantity tests
/// 2. IsInStock tests
/// 3. HasDiscount tests
/// </remarks>
public class ValidationExtensionsTests
{
    [Fact]
    public void IsValidQuantity_WithPositiveQuantity_ShouldReturnTrue()
    {
        var modelState = new ModelStateDictionary();

        var result = 2.IsValidQuantity(modelState);

        result.Should().BeTrue();
        modelState.Should().BeEmpty();
    }

    [Fact]
    public void IsValidQuantity_WithZeroQuantity_ShouldAddModelErrorAndReturnFalse()
    {
        var modelState = new ModelStateDictionary();

        var result = 0.IsValidQuantity(modelState, "Quantity");

        result.Should().BeFalse();
        modelState["Quantity"]!.Errors.Should().ContainSingle();
        modelState["Quantity"]!.Errors[0].ErrorMessage.Should().Be("Quantity must be greater than zero.");
    }

    [Fact]
    public void IsInStock_WithNullProduct_ShouldAddNotFoundError()
    {
        var modelState = new ModelStateDictionary();

        var result = ((ProductModel?)null)!.IsInStock(1, modelState);

        result.Should().BeFalse();
        modelState[string.Empty]!.Errors[0].ErrorMessage.Should().Be("Product not found.");
    }

    [Fact]
    public void IsInStock_WithInactiveProduct_ShouldAddUnavailableError()
    {
        var modelState = new ModelStateDictionary();
        var product = new ProductModel
        {
            IsActive = false,
            StockQuantity = 10
        };

        var result = product.IsInStock(1, modelState);

        result.Should().BeFalse();
        modelState[string.Empty]!.Errors[0].ErrorMessage.Should().Be("This product is no longer available.");
    }

    [Fact]
    public void IsInStock_WithInsufficientQuantity_ShouldAddStockError()
    {
        var modelState = new ModelStateDictionary();
        var product = new ProductModel
        {
            IsActive = true,
            StockQuantity = 3
        };

        var result = product.IsInStock(5, modelState);

        result.Should().BeFalse();
        modelState[string.Empty]!.Errors[0].ErrorMessage.Should().Be("Only 3 items available in stock.");
    }

    [Fact]
    public void IsInStock_WithSufficientQuantity_ShouldReturnTrue()
    {
        var modelState = new ModelStateDictionary();
        var product = new ProductModel
        {
            IsActive = true,
            StockQuantity = 8
        };

        var result = product.IsInStock(5, modelState);

        result.Should().BeTrue();
        modelState.Should().BeEmpty();
    }

    [Fact]
    public void HasDiscount_WithPositiveDiscount_ShouldReturnTrue()
    {
        var product = new ProductModel { DiscountPercent = 10m };

        product.HasDiscount().Should().BeTrue();
    }

    [Fact]
    public void HasDiscount_WithoutDiscount_ShouldReturnFalse()
    {
        var product = new ProductModel { DiscountPercent = 0m };

        product.HasDiscount().Should().BeFalse();
    }
}
