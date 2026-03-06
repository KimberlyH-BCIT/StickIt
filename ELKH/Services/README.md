# Services Layer (Business Logic)

This folder contains the **business logic layer** of the application. Services encapsulate domain logic, coordinate between repositories and controllers, and enforce business rules. All services are registered via dependency injection and follow interface-based abstraction for testability and maintainability.

---

## 📋 Service Overview

### Core Business Services

- **UserService / IUserService**: User management, caching, and lookup
- **CartService / ICartService**: Shopping cart, order placement, inventory
- **ProductService / IProductService**: Product CRUD, category, and search
- **SearchService / ISearchService**: Fuzzy search, autocomplete, and ranking
- **RatingService / IRatingService**: Product reviews, ratings, moderation
- **ModerationService / IModerationService**: Admin moderation, notifications

### Infrastructure & Supporting Services

- **SmtpEmailSender / IEmailSender**: SMTP email sending
- **EmailSenderAdapter**: Adapter for ASP.NET Identity email interface
- **CompiledQueries**: Precompiled EF Core queries for performance

### Background & Utility Services

- **FuzzyReindexService**: Hosted service for periodic search index refresh
- **FuzzyHelperService**: Singleton for fuzzy search and cache

---

## 🏗️ Registration & Dependency Injection

All services are registered in `Extensions/ServiceCollectionExtensions.cs`:

```csharp
services.AddScoped<IUserService, UserService>();
services.AddScoped<ICartService, CartService>();
services.AddScoped<IProductService, ProductService>();
services.AddScoped<ISearchService, SearchService>();
services.AddScoped<IRatingService, RatingService>();
services.AddScoped<IModerationService, ModerationService>();
services.AddScoped<SmtpEmailSender>();
services.AddScoped<EmailSenderAdapter>();
services.AddSingleton<FuzzyHelperService>();
services.AddSingleton<FuzzyReindexService>();
services.AddHostedService(sp => sp.GetRequiredService<FuzzyReindexService>());
```

- **Scoped**: Most business services (per-request lifetime)
- **Singleton**: Background/utility services (one instance for app lifetime)

---

## 📝 Service Documentation & Patterns

- **All services and interfaces have XML documentation** for IntelliSense and maintainability.
- **Interface-based abstraction**: All services have interfaces for testability and flexibility.
- **Options pattern**: Configuration is injected via strongly-typed options classes (e.g., `EmailOptions`, `CacheOptions`).
- **Security**: Services validate user ownership, use secure defaults, and avoid direct HttpContext access.
- **Async/await**: All data access and business logic is asynchronous.

---

## 🔑 Key Service Responsibilities

### UserService
- User lookup by email or ID
- 10-minute in-memory cache for user data
- Used by CartService, OrderService, etc.

### CartService
- Add/remove items, quick "Buy Now"
- Atomic order placement with transaction safety
- Inventory and ownership validation

### ProductService
- CRUD for products
- Category management
- Product search and filtering

### SearchService
- Fuzzy search with multi-tier fallback
- Uses FuzzyHelperService for fast cache lookups
- Returns match positions for UI highlighting

### RatingService
- Add/edit/delete product ratings
- Approve/flag ratings for moderation
- One rating per user per product

### ModerationService
- Admin notifications for flagged content
- Secure URL generation for moderation actions
- Configurable admin email list

### SmtpEmailSender / EmailSenderAdapter
- SMTP email sending with secure configuration
- Adapter bridges ASP.NET Identity and custom email interface

### FuzzyReindexService
- Periodic refresh of fuzzy search/autocomplete index
- Runs as a hosted background service

### FuzzyHelperService
- Singleton for fuzzy search cache and algorithms
- Used by SearchService and FuzzyReindexService

### CompiledQueries
- Precompiled EF Core queries for hot paths (user lookup, etc.)

---

## 🛡️ Security & Best Practices

- **User ownership validation**: All user-facing services enforce that users can only access their own data.
- **Transaction safety**: Order placement and other critical operations use explicit transactions.
- **No business logic in controllers**: Controllers delegate to services for all business rules.
- **No direct HttpContext access**: All data is passed as parameters for testability.
- **No static methods**: All services are DI-friendly and testable.
- **Comprehensive XML docs**: All public methods and interfaces are documented.

---

## 🧪 Testing

- **Unit testing**: All services can be tested in isolation by mocking dependencies.
- **Integration testing**: Use in-memory EF Core for end-to-end service tests.

---

## 📝 Adding a New Service

1. Create an interface (e.g., `IMyService.cs`)
2. Create an implementation (e.g., `MyService.cs`)
3. Register in DI (`ServiceCollectionExtensions.cs`)
4. Inject and use in controllers or other services
5. Add XML documentation for all public members

---

## 📚 Related Docs
- See `ARCHITECTURE.md` for system overview
- See `Controllers/` for presentation layer
- See `Repositories/` for data access patterns
- See `Configuration/` for options classes

---

**Last Updated:** 2026  
**Status:** ✅ Production-Ready
