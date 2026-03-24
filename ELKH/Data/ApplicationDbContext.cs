using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ELKH.Models;

namespace ELKH.Data
{ 

    /// <summary>
    /// Entity Framework Core database context for the application.
    /// Manages all entity sets, relationships, indexes, and custom model configuration.
    /// Inherits from IdentityDbContext to support ASP.NET Core Identity features.
    /// </summary>
    public class ApplicationDbContext : IdentityDbContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ApplicationDbContext"/> class.
        /// </summary>
        /// <param name="options">The options to be used by a <see cref="DbContext"/>.</param>
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<ProductModel> Products { get; set; }
        public DbSet<CategoryModel> Categories { get; set; }
        public DbSet<ProductImageModel> ProductImage { get; set; }
        public DbSet<RegisteredUserModel> RegisteredUsers { get; set; }
        public DbSet<CartModel> Carts { get; set; }
        public DbSet<OrderModel> Orders { get; set; }
        public DbSet<OrderItemModel> OrderItems { get; set; }
        public DbSet<TransactionModel> Transactions { get; set; }
        public DbSet<ContactDetailModel> ContactDetails { get; set; }
        public DbSet<ProductRatingModel> ProductRatings { get; set; }
        public DbSet<StoreReviewModel> StoreReviews { get; set; }
        public DbSet<WishListModel> WishLists { get; set; }
        public DbSet<WishListItemModel> WishListItems { get; set; }
        public DbSet<UserLogModel> UserLogs { get; set; }
        public DbSet<UserProfileModel> UserProfiles { get; set; }
        public DbSet<FuzzySuggestionModel> FuzzySuggestions { get; set; }
        public DbSet<AuditEntryModel> AuditEntries { get; set; }
        public DbSet<CachedFuzzyKeyModel> CachedFuzzyKeys { get; set; }
        public DbSet<StockNotificationModel> StockNotifications { get; set; }

        /// <summary>
        /// Configures entity relationships, indexes, and table mappings for the application.
        /// </summary>
        /// <param name="modelBuilder">The builder being used to construct the model for this context.</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // One-to-one: RegisteredUser <-> WishList (each user has a single wishlist)
            modelBuilder.Entity<RegisteredUserModel>()
                .HasOne(r => r.WishLists)
                .WithOne(w => w.RegisteredUser)
                .HasForeignKey<WishListModel>(w => w.FkUserId);

            // Many-to-many: WishList -> Products via WishListItemModel.
            // UsingEntity consolidates the two separate WishListItem relationship configs
            // into a single declaration and exposes a direct Products skip-navigation on
            // WishListModel so callers can query wishlist.Products without going through
            // WishListItems. No schema changes — the existing join table is reused.
            modelBuilder.Entity<WishListModel>()
                .HasMany(w => w.Products)
                .WithMany()
                .UsingEntity<WishListItemModel>(
                    r => r.HasOne(wi => wi.Product)
                          .WithMany(p => p.WishListItems)
                          .HasForeignKey(wi => wi.FkProductId)
                          .OnDelete(DeleteBehavior.Cascade),
                    l => l.HasOne(wi => wi.WishList)
                          .WithMany(w => w.WishListItems)
                          .HasForeignKey(wi => wi.FkWishListId)
                          .OnDelete(DeleteBehavior.Cascade)
                );

            // One-to-one: Order <-> Transaction (each order has a single payment transaction)
            modelBuilder.Entity<OrderModel>()
                .HasOne(o => o.Transaction)
                .WithOne(t => t.Order)
                .HasForeignKey<TransactionModel>(t => t.FkOrderId);

            // Index on normalized product name for fast fuzzy search and autocomplete
            modelBuilder.Entity<ProductModel>()
                .HasIndex(p => p.NameNormalized);

            // Table mappings for audit and fuzzy search infrastructure
            modelBuilder.Entity<ELKH.Models.AuditEntryModel>().ToTable("AuditEntries");
            modelBuilder.Entity<ELKH.Models.CachedFuzzyKeyModel>().ToTable("CachedFuzzyKeys");
            modelBuilder.Entity<ELKH.Models.FuzzySuggestionModel>().ToTable("FuzzySuggestions");

            // Index on normalized name for fuzzy suggestions (improves search performance)
            modelBuilder.Entity<ELKH.Models.FuzzySuggestionModel>()
                .HasIndex(f => f.NameNormalized);

            // Many-to-one: FuzzySuggestion -> Product (each suggestion is linked to a product)
            modelBuilder.Entity<ELKH.Models.FuzzySuggestionModel>()
                .HasOne<ELKH.Models.ProductModel>()
                .WithMany(p => p.FuzzySuggestions)
                .HasForeignKey(f => f.PkProductId)
                .OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Cascade);

            // ── Explicit FK bindings ─────────────────────────────────────────────
            // Without HasForeignKey, EF Core creates a shadow FK column alongside
            // every explicit FkXxx property in the model, resulting in two FK columns
            // per relationship (e.g. FkCategoryId AND CategoryPkCategoryId).
            // These bindings tell EF to use the existing model properties so that
            // new databases are created with a single FK column per relationship.
            // Existing databases keep the orphaned shadow columns (harmless / ignored).

            modelBuilder.Entity<ProductModel>()
                .HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.FkCategoryId);

            modelBuilder.Entity<CartModel>()
                .HasOne(c => c.RegisteredUser)
                .WithMany(u => u.Cart)
                .HasForeignKey(c => c.FkRegisteredUserId);

            modelBuilder.Entity<CartModel>()
                .HasOne(c => c.Product)
                .WithMany(p => p.Carts)
                .HasForeignKey(c => c.FkProductID);

            modelBuilder.Entity<ContactDetailModel>()
                .HasOne(c => c.RegisteredUser)
                .WithMany(u => u.ContactDetails)
                .HasForeignKey(c => c.FkRegisteredUserId);

            modelBuilder.Entity<OrderModel>()
                .HasOne(o => o.RegisteredUser)
                .WithMany(u => u.Orders)
                .HasForeignKey(o => o.FkRegisteredUserId);

            modelBuilder.Entity<OrderModel>()
                .HasOne(o => o.ContactDetail)
                .WithMany()
                .HasForeignKey(o => o.FkContactId);

            modelBuilder.Entity<OrderItemModel>()
                .HasOne(oi => oi.Order)
                .WithMany(o => o.OrderItems)
                .HasForeignKey(oi => oi.FkOrderId);

            modelBuilder.Entity<OrderItemModel>()
                .HasOne(oi => oi.Product)
                .WithMany()
                .HasForeignKey(oi => oi.FkProductId);

            modelBuilder.Entity<ProductRatingModel>()
                .HasOne(r => r.Products)
                .WithMany(p => p.ProductRatings)
                .HasForeignKey(r => r.FkProductId);

            modelBuilder.Entity<ProductRatingModel>()
                .HasOne(r => r.RegisteredUser)
                .WithMany(u => u.ProductRatings)
                .HasForeignKey(r => r.FkRegisteredUserId);

            modelBuilder.Entity<StoreReviewModel>()
                .HasOne(sr => sr.RegisteredUser)
                .WithMany(u => u.StoreReviews)
                .HasForeignKey(sr => sr.FkRegisteredUserId);

            modelBuilder.Entity<TransactionModel>()
                .HasOne(t => t.ContactDetail)
                .WithMany()
                .HasForeignKey(t => t.FkContactId);

            // ══════════════════════════════════════════════════════════════════════
            // PERFORMANCE INDEXES
            // Critical indexes for frequently queried foreign keys and composite queries
            // Added to improve query performance and prevent table scans
            // ══════════════════════════════════════════════════════════════════════

            // Index on ProductRatingModel.FkProductId for product review queries
            // Improves: "Get all ratings for product X" queries (product details page)
            modelBuilder.Entity<ProductRatingModel>()
                .HasIndex(r => r.FkProductId)
                .HasDatabaseName("IX_ProductRatings_FkProductId");

            // Composite index on OrderModel for user order history queries
            // Improves: "Get orders for user X sorted by date" queries (order history page)
            // Covering index: UserId + CreatedAt allows ORDER BY without separate sort operation
            modelBuilder.Entity<OrderModel>()
                .HasIndex(o => new { o.FkRegisteredUserId, o.CreatedAt })
                .HasDatabaseName("IX_Orders_UserId_CreatedAt");

            // Index on TransactionModel.TransactionStatus for payment tracking queries
            // Improves: "Get all pending/completed/failed transactions" (admin dashboards)
            modelBuilder.Entity<TransactionModel>()
                .HasIndex(t => t.TransactionStatus)
                .HasDatabaseName("IX_Transactions_TransactionStatus");

            // Index on WishListItemModel.FkWishListId for wishlist item retrieval
            // Improves: "Get all items in wishlist X" queries (wishlist page)
            modelBuilder.Entity<WishListItemModel>()
                .HasIndex(wi => wi.FkWishListId)
                .HasDatabaseName("IX_WishListItems_FkWishListId");

            // Index on OrderModel.CreatedAt for date range queries
            // Improves: Sales analytics queries filtered by date range
            modelBuilder.Entity<OrderModel>()
                .HasIndex(o => o.CreatedAt)
                .HasDatabaseName("IX_Orders_CreatedAt");

            // Composite index on ProductRatingModel for moderation queries
            // Improves: "Get approved/pending reviews sorted by date" (moderation page, product details)
            // Filters on Approved + IsFlagged, then sorts by RatedTime
            modelBuilder.Entity<ProductRatingModel>()
                .HasIndex(r => new { r.Approved, r.IsFlagged, r.RatedTime })
                .HasDatabaseName("IX_ProductRatings_Approved_Flagged_RatedTime");

            // Composite index on StoreReviewModel for homepage review queries
            // Improves: "Get approved store reviews sorted by date" (homepage carousel)
            // Filters on Approved + IsDeleted, then sorts by CreatedAt
            modelBuilder.Entity<StoreReviewModel>()
                .HasIndex(sr => new { sr.Approved, sr.IsDeleted, sr.CreatedAt })
                .HasDatabaseName("IX_StoreReviews_Approved_Deleted_CreatedAt");
        }

    }

}
