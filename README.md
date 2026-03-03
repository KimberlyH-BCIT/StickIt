# StickIt (ELKH)

A full-featured e-commerce web application built with **ASP.NET Core 10** and **.NET 10**.  
Supports product catalog browsing, shopping cart, order management, user profiles, wishlists, product reviews, and a complete admin moderation and audit area.

---

## Features

- **Product catalog** — browsing, search (multi-tier fuzzy + FTS5), and detail pages with reviews
- **Shopping cart** — add/remove items, apply discounts, and place orders with atomic stock validation
- **Buy Now** — single-click order placement bypassing the cart
- **Order management** — order history with status tracking and delivery status
- **User profiles** — avatar upload, address book, login history, and dashboard
- **Wishlist** — add/remove products with sort options (newest, on-sale, most popular)
- **Product reviews** — submit, edit, and soft-delete ratings tied to fulfilled order items; 24 h re-submission cooldown; 7-day edit cooldown
- **Admin area** — moderation console, audit log viewer with CSV export, metrics dashboard
- **Fuzzy search autocomplete** — 4-tier fallback: cache → precomputed suggestions → SQLite FTS5 → FuzzySharp scoring; background reindex every 6 hours
- **Email** — SMTP sender for production; file-based dev sender writes `.eml.txt` files locally
- **Localization** — `en-CA` / `fr-CA` with runtime culture switching
- **Security** — HSTS, HTTPS redirection, security response headers, anti-forgery validation, Identity email confirmation, per-moderator rate limiting

---

## Technology Stack

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core 10 (.NET 10, C# 14) |
| UI | MVC Controllers + Views, Razor Pages (Identity) |
| Database | SQLite via Entity Framework Core (Code-First) |
| ORM | EF Core with compiled queries and migrations |
| Auth | ASP.NET Core Identity |
| Mapping | AutoMapper |
| Search | FuzzySharp, SQLite FTS5 |
| Caching | `IMemoryCache` + Output Cache (tag-based invalidation) |
| Compression | Response Compression (Gzip / Brotli) |
| Email | `SmtpClient` (prod), `FileEmailSender` (dev) |
| Testing | xUnit + EF Core InMemory |

---

## Project Structure

```
StickIt/
├── ELKH/                              # Main application project
│   ├── Areas/
│   │   ├── Admin/                     # Admin-only controllers & views
│   │   │   └── Controllers/
│   │   │       ├── AuditController.cs
│   │   │       ├── MetricsController.cs
│   │   │       └── ModerationController.cs
│   │   └── Identity/                  # Scaffolded Identity pages
│   ├── Configuration/                 # Strongly-typed options (Cache, Email, Search, Moderation)
│   ├── Constants/                     # ModerationRoutes, RatingConstants
│   ├── Controllers/                   # MVC controllers (Product, Cart, Order, User, Wishlist…)
│   ├── Data/
│   │   ├── ApplicationDbContext.cs
│   │   └── DbSeeder.cs                # Demo product seed (no-op if data exists)
│   ├── Extensions/
│   │   ├── ApplicationBuilderExtensions.cs   # Middleware pipeline & security headers
│   │   ├── ServiceCollectionExtensions.cs    # DI registration groups
│   │   └── ValidationExtensions.cs
│   ├── Mapping/
│   │   └── AutoMapperProfile.cs
│   ├── Migrations/                    # EF Core migrations
│   ├── Models/                        # Domain entities (14 models)
│   ├── Repositories/                  # Repository pattern over EF Core
│   ├── Services/                      # Business logic & background services
│   ├── ViewModels/                    # Presentation DTOs
│   ├── Views/                         # Razor views
│   ├── wwwroot/                       # Static assets (CSS, JS, images)
│   ├── appsettings.json               # Base configuration (no secrets)
│   └── Program.cs
└── ELKH.Tests/                        # xUnit unit tests
    ├── ModerationControllerTests.cs
    └── RatingServiceTests.cs
```

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Git

No external database server required — the project uses **SQLite** out of the box.

---

## Quick Start

### 1. Clone

```bash
git clone https://github.com/Velyene/StickIt.git
cd StickIt
```

### 2. Restore and build

```bash
dotnet restore
dotnet build
```

### 3. Configure secrets (development)

Sensitive values (SMTP credentials, etc.) must **never** be committed.  
Use the [Secret Manager](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) in development:

```bash
cd ELKH
dotnet user-secrets set "Email:User" "you@example.com"
dotnet user-secrets set "Email:Pass" "yourpassword"
```

For local dev with no SMTP server, the application falls back to `FileEmailSender`, which writes email content to `ELKH/SavedEmails/` (already in `.gitignore`).

### 4. Apply database migrations

Migrations run automatically on startup in the Development environment.  
To apply manually:

```bash
dotnet ef database update --project ELKH --startup-project ELKH
```

### 5. Run

```bash
dotnet run --project ELKH
```

Open the URL printed to the console (typically `https://localhost:7239`).  
Demo products are seeded automatically on first run.

---

## Configuration

All non-secret configuration lives in `ELKH/appsettings.json`.  
Override per environment using `appsettings.Development.json` (gitignored) or environment variables.

| Section | Key | Purpose |
|---|---|---|
| `ConnectionStrings` | `DefaultConnection` | SQLite connection string |
| `AllowedHosts` | — | Restrict host headers; set via `ASPNETCORE_AllowedHosts` in production |
| `Email` | `Host`, `Port`, `EnableSsl`, `User`, `Pass`, `From` | SMTP settings (`User`/`Pass` via secrets only) |
| `Moderation` | `AdminEmails`, `BaseUrl` | Flag notification recipients and absolute URL base |
| `Cache` | `UserLookupExpirationMinutes` | User cache TTL |
| `Search:Fuzzy` | `CandidateLimit`, `TopResults`, `PartialRatioThreshold` | Fuzzy search tuning |
| `Search` | `ReindexIntervalMinutes` | Background FTS reindex interval (default: 360 min) |
| `Database` | `ApplyMigrationsOnStartup`, `AllowMigrationInProduction` | Startup migration behaviour |
| `Localization` | `DefaultCulture`, `SupportedCultures` | UI cultures (`en-CA`, `fr-CA`) |

---

## Testing

Unit tests are in `ELKH.Tests` and use **xUnit** with the **EF Core InMemory** provider.

```bash
dotnet test ELKH.Tests
```

Tests cover:
- `ModerationController` — approve and flag actions (AJAX + standard form paths)
- `RatingService` — create/edit/delete business rules, eligibility, cooldowns

---

## Architecture

The application follows a layered, interface-driven architecture:

```
HTTP Request
     │
     ▼
Controller / Razor Page        ← thin; delegates immediately to services
     │
     ▼
Service (IXxxService)          ← business rules, caching, transactions
     │
     ▼
Repository / DbContext         ← data access; compiled queries for hot paths
     │
     ▼
SQLite (EF Core)
```

Key design decisions:

- **Services are scoped and interface-backed** — all injected via constructor, fully testable.
- **CompiledQueries** — EF Core compiled async queries on hot paths (user lookup, product detail).
- **4-tier search** — result cache → `FuzzySuggestions` table → SQLite FTS5 → FuzzySharp fallback.
- **Atomic order placement** — explicit `BeginTransactionAsync` in `CartService.PlaceOrderAsync`; stock validation runs before the transaction opens to minimise lock time.
- **Output Cache with tags** — product listings and details are cache-tagged `"products"` and invalidated on write operations.
- **Background FTS reindex** — `FuzzyReindexService` (`IHostedService`) rebuilds the FTS virtual table and precomputed suggestion rows on startup and periodically.

---

## Security

| Measure | Detail |
|---|---|
| HSTS | Enforced in non-Development environments |
| HTTPS redirection | All HTTP requests redirected |
| Security headers | `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy` on every response |
| Anti-forgery | `[ValidateAntiForgeryToken]` on all state-changing POST actions |
| Identity | Email confirmation required before sign-in; roles used for admin access |
| Secrets | SMTP credentials via `dotnet user-secrets` (dev) or environment variables / Key Vault (prod) |
| Moderation rate limiting | Per-moderator flag cooldown enforced via `IMemoryCache` |
| Open-redirect prevention | `ModerationRoutes.GetSafeBaseUrl()` validates base URLs from configuration only |
| `.gitignore` | `appsettings.*.json`, `SavedEmails/`, `*.db`, certificates, and `.env` files all excluded |

---

## Coding Conventions

- **Controllers stay thin** — all business logic lives in `ELKH/Services`.
- **PascalCase** for public types and members; **camelCase** for private fields and locals.
- **Pk/Fk prefixes** on model keys (`PkProductId`, `FkUserId`) are a legacy convention — do not rename in isolated PRs; follow the coordinated rename plan on a dedicated migration branch.
- **Navigation properties** — singular for a single entity (`Product`), plural for collections (`OrderItems`).
- **XML docs** on all public service interfaces and non-trivial implementations.
- **`/// <inheritdoc/>`** on concrete service method implementations that match their interface doc.

---

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for branching strategy, commit message style, and PR requirements.

Short version:
1. Branch off `WIP-Kimberly`: `git checkout -b feature/your-feature`
2. Keep PRs focused — one logical change per PR
3. Add or update tests for behavioural changes
4. Run `dotnet build` and `dotnet test` before opening a PR

---

## Repository

- **GitHub:** <https://github.com/Velyene/StickIt>
- **Active branch:** `WIP-Kimberly`
- **Namespace:** `ELKH`

