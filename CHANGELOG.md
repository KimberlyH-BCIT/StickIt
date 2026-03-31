# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Comprehensive project analysis and scoreboard documentation
- CONTRIBUTING.md with detailed contribution guidelines
- This CHANGELOG.md for tracking version history

### Changed
- Removed redundant ViewBag assignments in ProductController
- Removed duplicate AddVersionedApiExplorer registration in Program.cs

### Fixed
- Fixed "Repostories" typo in .csproj file (now "Repositories")

---

## [2.0.0] - 2026-03-24

### Added
- **PWA Support**: Progressive Web App with service worker and offline capabilities
- **Kawaii UI Theme**: New pastel-colored, friendly interface design
- **Structured Logging**: Enhanced logging with StructuredLoggingService and correlation IDs
- **Image Optimization**: ImageOptimizationService with lazy loading and WebP support
- **Global Exception Middleware**: Centralized error handling with Application Insights integration
- **Store Reviews**: New store-level review system with moderation
- **Stock Notifications**: Email alerts when products come back in stock
- **Health Checks**: Database, PayPal, and Email health check endpoints

### Changed
- Upgraded to .NET 10 from .NET 8
- Replaced AutoMapper with manual ProductMapper (security improvement)
- Enhanced Content Security Policy headers
- Improved accessibility with WCAG 2.1 AA compliance (Phases 1-3 complete)
- Updated all NuGet packages to latest stable versions

### Security
- Added rate limiting on authentication and search endpoints
- Implemented binary signature validation for image uploads
- Enhanced CSRF protection with meta tag for AJAX requests
- Added security headers (X-Content-Type-Options, X-Frame-Options, Referrer-Policy)

---

## [1.5.0] - 2026-02-15

### Added
- **API Versioning**: Support for v1 and v2 API endpoints
- **Swagger Documentation**: OpenAPI documentation with versioned endpoints
- **Fuzzy Search**: FuzzySharp integration for intelligent product search
- **Background Reindexing**: Automatic search index maintenance
- **Order Email Service**: Automated order confirmation emails

### Changed
- Refactored ProductService to use compiled queries
- Improved cart AJAX operations with better error handling
- Enhanced product details page with review sorting and pagination

### Fixed
- Fixed cart quantity update race conditions
- Fixed search autocomplete accessibility issues
- Fixed mobile navigation menu z-index conflicts

---

## [1.4.0] - 2026-01-20

### Added
- **Wishlist Feature**: Save products for future purchase
- **Product Ratings**: Customer review and rating system
- **Rating Moderation**: Admin tools for review approval/rejection
- **User Dashboard**: Personalized dashboard with order history

### Changed
- Redesigned product detail page layout
- Improved category navigation structure
- Enhanced mobile responsiveness

### Fixed
- Fixed price calculation with discount percentages
- Fixed order total rounding issues

---

## [1.3.0] - 2025-12-10

### Added
- **PayPal Integration**: Secure payment processing
- **Checkout Flow**: Multi-step checkout with address selection
- **Order Tracking**: Order status updates and history
- **Transaction Records**: Complete payment audit trail

### Changed
- Updated Bootstrap to version 5.3
- Improved form validation messages
- Enhanced loading states for async operations

### Security
- Added PayPal webhook signature verification
- Implemented payment idempotency keys

---

## [1.2.0] - 2025-11-01

### Added
- **Admin Dashboard**: Sales analytics and system overview
- **Inventory Management**: Stock tracking with image upload
- **Role Management**: Create and assign user roles
- **Audit Logging**: Track administrative actions

### Changed
- Reorganized admin views into dedicated area
- Improved data table pagination and sorting

### Fixed
- Fixed admin user creation validation
- Fixed role assignment persistence

---

## [1.1.0] - 2025-10-15

### Added
- **Product Catalog**: Browse and filter products
- **Category System**: Organize products by category
- **Shopping Cart**: Add/remove items with quantity management
- **User Registration**: Account creation with email confirmation

### Changed
- Initial UI styling with Bootstrap 5
- Basic responsive layout implementation

---

## [1.0.0] - 2025-09-01

### Added
- Initial project setup with ASP.NET Core 8 Razor Pages
- Entity Framework Core with SQLite database
- ASP.NET Core Identity for authentication
- Basic project structure with layered architecture
- Development environment configuration

---

## Version History Summary

| Version | Date | Highlights |
|---------|------|------------|
| 2.0.0 | 2026-03-24 | .NET 10, PWA, Kawaii UI, Enhanced Security |
| 1.5.0 | 2026-02-15 | API Versioning, Fuzzy Search, Swagger |
| 1.4.0 | 2026-01-20 | Wishlists, Ratings, User Dashboard |
| 1.3.0 | 2025-12-10 | PayPal Integration, Checkout Flow |
| 1.2.0 | 2025-11-01 | Admin Dashboard, Inventory Management |
| 1.1.0 | 2025-10-15 | Product Catalog, Shopping Cart |
| 1.0.0 | 2025-09-01 | Initial Release |

---

[Unreleased]: https://github.com/Velyene/StickIt/compare/v2.0.0...HEAD
[2.0.0]: https://github.com/Velyene/StickIt/compare/v1.5.0...v2.0.0
[1.5.0]: https://github.com/Velyene/StickIt/compare/v1.4.0...v1.5.0
[1.4.0]: https://github.com/Velyene/StickIt/compare/v1.3.0...v1.4.0
[1.3.0]: https://github.com/Velyene/StickIt/compare/v1.2.0...v1.3.0
[1.2.0]: https://github.com/Velyene/StickIt/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/Velyene/StickIt/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/Velyene/StickIt/releases/tag/v1.0.0
