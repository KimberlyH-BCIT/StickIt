using System;
using System.Collections.Generic;
using System.Linq;
using Bogus;
using ELKH.Models;

namespace ELKH.Tests.Utilities;

/// <summary>
/// Test data generators using Bogus library for creating realistic test data.
/// Provides consistent, repeatable test data for all test scenarios.
/// </summary>
public static class TestDataFactory
{
    private static readonly Faker _faker = new Faker();

    // ================================================================
    // User & Profile Test Data
    // ================================================================

    /// <summary>
    /// Creates a test user with default or specified properties
    /// </summary>
    public static RegisteredUserModel CreateUser(
        int? id = null, 
        string? email = null)
    {
        return new RegisteredUserModel
        {
            PkRegisteredUserId = id ?? _faker.Random.Int(1, 10000),
            Email = email ?? _faker.Internet.Email()
        };
    }

    // ================================================================
    // Product & Category Test Data
    // ================================================================

    /// <summary>
    /// Creates a test product with realistic e-commerce data
    /// </summary>
    public static ProductModel CreateProduct(
        int? id = null,
        int? categoryId = null,
        decimal? price = null,
        int? stockQuantity = null,
        bool isActive = true)
    {
        return new ProductModel
        {
            PkProductId = id ?? _faker.Random.Int(1, 10000),
            Name = _faker.Commerce.ProductName(),
            Description = _faker.Commerce.ProductDescription(),
            Price = price ?? decimal.Parse(_faker.Commerce.Price(5, 100)),
            FkCategoryId = categoryId ?? _faker.Random.Int(1, 10),
            StockQuantity = stockQuantity ?? _faker.Random.Int(0, 1000),
            IsActive = isActive
        };
    }

    /// <summary>
    /// Creates a test category with hierarchical support
    /// </summary>
    public static CategoryModel CreateCategory(
        int? id = null,
        string? name = null)
    {
        return new CategoryModel
        {
            PkCategoryId = id ?? _faker.Random.Int(1, 100),
            CategoryName = name ?? _faker.Commerce.Department()
        };
    }

    // ================================================================
    // Order & Cart Test Data
    // ================================================================

    /// <summary>
    /// Creates a test cart item for shopping cart functionality
    /// </summary>
    public static CartModel CreateCartItem(
        int? userId = null,
        int? productId = null,
        int quantity = 1)
    {
        return new CartModel
        {
            PkCartId = _faker.Random.Int(1, 10000),
            FkRegisteredUserId = userId ?? _faker.Random.Int(1, 1000),
            FkProductID = productId ?? _faker.Random.Int(1, 1000),
            Quantity = quantity
        };
    }

    // ================================================================
    // Collection Generators
    // ================================================================

    /// <summary>
    /// Creates a collection of test users
    /// </summary>
    public static List<RegisteredUserModel> CreateUsers(int count)
    {
        return Enumerable.Range(1, count)
            .Select(i => CreateUser(id: i))
            .ToList();
    }

    /// <summary>
    /// Creates a collection of test products
    /// </summary>
    public static List<ProductModel> CreateProducts(int count, int? categoryId = null)
    {
        return Enumerable.Range(1, count)
            .Select(i => CreateProduct(id: i, categoryId: categoryId))
            .ToList();
    }

    /// <summary>
    /// Creates a collection of test categories
    /// </summary>
    public static List<CategoryModel> CreateCategories(int count)
    {
        return Enumerable.Range(1, count)
            .Select(i => CreateCategory(id: i))
            .ToList();
    }
}