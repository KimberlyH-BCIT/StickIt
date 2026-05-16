using ELKH.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Data
{
    /// <summary>
    /// Entity Framework Core database context for the StickIt e-commerce application.
    /// Manages entity sets, relationships, indexes, and Identity integration.
    /// </summary>
    public class ApplicationDbContext : IdentityDbContext
    {
        #region Constructor & Initialization

        /// <summary>
        /// Initializes a new instance of the <see cref="ApplicationDbContext"/> class.
        /// </summary>
        /// <param name="options">The options to be used by a <see cref="DbContext"/>.</param>
        /// <remarks>
        /// The context is configured through dependency injection in Program.cs with:
        /// • SQLite database provider for development and lightweight deployment
        /// • Connection string validation for fail-fast startup behavior
        /// • Identity integration for user management and authentication
        /// </remarks>
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        #endregion

        #region Entity Set Declarations

        // ── Core Product Catalog ──
        /// <summary>Products in the e-commerce catalog with pricing, categories, and availability.</summary>
        public virtual DbSet<ProductModel> Products { get; set; } = null!;

        /// <summary>Product categories for catalog organization and filtering.</summary>
        public DbSet<CategoryModel> Categories { get; set; } = null!;

        /// <summary>Product images with optimization metadata and storage references.</summary>
        public DbSet<ProductImageModel> ProductImage { get; set; } = null!;

        // ── User Management & Profiles ──
        /// <summary>Registered users extending ASP.NET Core Identity with e-commerce specific data.</summary>
        public DbSet<RegisteredUserModel> RegisteredUsers { get; set; } = null!;

        /// <summary>Extended user profiles with preferences and demographic information.</summary>
        public DbSet<UserProfileModel> UserProfiles { get; set; } = null!;

        /// <summary>User activity logs for security monitoring and analytics.</summary>
        public DbSet<UserLogModel> UserLogs { get; set; } = null!;

        // ── Commerce & Orders ──
        /// <summary>Shopping carts with temporary item storage before checkout.</summary>
        public DbSet<CartModel> Carts { get; set; } = null!;

        /// <summary>Customer orders with shipping information and status tracking.</summary>
        public virtual DbSet<OrderModel> Orders { get; set; } = null!;

        /// <summary>Individual line items within orders, linking products with quantities and pricing.</summary>
        public DbSet<OrderItemModel> OrderItems { get; set; } = null!;

        /// <summary>Payment transactions with gateway integration and financial reconciliation data.</summary>
        public virtual DbSet<TransactionModel> Transactions { get; set; } = null!;

        /// <summary>Shipping and billing addresses for orders and user profiles.</summary>
        public DbSet<ContactDetailModel> ContactDetails { get; set; } = null!;

        /// <summary>Available shipping methods with pricing and delivery timeframes.</summary>
        public DbSet<ShippingMethodModel> ShippingMethods { get; set; } = null!;

        // ── Promotions & Coupons ──
        /// <summary>Discount coupons for promotional campaigns and customer retention.</summary>
        public DbSet<CouponModel> Coupons { get; set; } = null!;

        /// <summary>Junction table tracking which coupons were applied to specific orders.</summary>
        public DbSet<OrderCouponModel> OrderCoupons { get; set; } = null!;

        // ── Reviews & Social Features ──
        /// <summary>Customer product ratings and reviews with moderation support.</summary>
        public DbSet<ProductRatingModel> ProductRatings { get; set; } = null!;

        /// <summary>Overall store reviews and feedback for business improvement.</summary>
        public DbSet<StoreReviewModel> StoreReviews { get; set; } = null!;

        // ── Personalization & Wishlists ──
        /// <summary>User wishlists for saving products for future purchase.</summary>
        public DbSet<WishListModel> WishLists { get; set; } = null!;

        /// <summary>Individual items within wishlists, linking users to desired products.</summary>
        public DbSet<WishListItemModel> WishListItems { get; set; } = null!;

        // ── Search & Performance Infrastructure ──
        /// <summary>Fuzzy search suggestions with relevance scoring and caching.</summary>
        public DbSet<FuzzySuggestionModel> FuzzySuggestions { get; set; } = null!;

        /// <summary>Cached fuzzy search keys for performance optimization.</summary>
        public DbSet<CachedFuzzyKeyModel> CachedFuzzyKeys { get; set; } = null!;
        public DbSet<StaffMessageModel> StaffMessages { get; set; } = null!;
        public DbSet<MessageReplyModel> MessageReplies { get; set; } = null!;

        // ── Infrastructure & Monitoring ──
        /// <summary>Audit trail entries for compliance and security monitoring.</summary>
        public DbSet<AuditEntryModel> AuditEntries { get; set; } = null!;

        /// <summary>Stock notification requests for out-of-stock product alerts.</summary>
        public DbSet<StockNotificationModel> StockNotifications { get; set; } = null!;

        #endregion

        #region Entity Relationship Configuration

        /// <summary>
        /// Configures entity relationships, indexes, and table mappings for the e-commerce application.
        /// </summary>
        /// <param name="modelBuilder">The builder being used to construct the model for this context.</param>
        /// <remarks>
        /// This method defines the complete data model structure including:
        /// • Relationship cardinalities and foreign key constraints
        /// • Database indexes for query performance optimization
        /// • Custom table mappings for infrastructure entities
        /// • Cascade delete behaviors for data consistency
        /// 
        /// RELATIONSHIP PATTERNS:
        /// • One-to-one: User ↔ Wishlist, Order ↔ Transaction
        /// • One-to-many: Product → OrderItems, User → Orders
        /// • Many-to-many: Wishlist ↔ Products (via WishListItems junction)
        /// 
        /// PERFORMANCE OPTIMIZATIONS:
        /// • Normalized name indexes enable fast fuzzy search and autocomplete
        /// • Strategic foreign key indexes minimize join operation overhead
        /// • Efficient junction table configuration for many-to-many relationships
        /// </remarks>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Initialize base Identity configuration (AspNetUsers, AspNetRoles, etc.)
            base.OnModelCreating(modelBuilder);

            #region Core Relationship Configuration

            // ── One-to-One: User ↔ Wishlist ──
            // Each registered user has exactly one wishlist for saved products
            // Business rule: Wishlist creation is automatic on user registration
            modelBuilder.Entity<RegisteredUserModel>()
                .HasOne(r => r.WishLists)
                .WithOne(w => w.RegisteredUser)
                .HasForeignKey<WishListModel>(w => w.FkUserId)
                .OnDelete(DeleteBehavior.Cascade); // Remove wishlist when user is deleted

            // ── Many-to-Many: Wishlist ↔ Products via Junction Entity ──
            // Allows users to save multiple products and products to be in multiple wishlists
            // UsingEntity provides direct navigation while maintaining explicit junction control
            modelBuilder.Entity<WishListModel>()
                .HasMany(w => w.Products)
                .WithMany()
                .UsingEntity<WishListItemModel>(
                    // Configure Product side of relationship
                    r => r.HasOne(wi => wi.Product)
                          .WithMany(p => p.WishListItems)
                          .HasForeignKey(wi => wi.FkProductId)
                          .OnDelete(DeleteBehavior.Cascade), // Remove wishlist item when product deleted

                    // Configure Wishlist side of relationship  
                    l => l.HasOne(wi => wi.WishList)
                          .WithMany(w => w.WishListItems)
                          .HasForeignKey(wi => wi.FkWishListId)
                          .OnDelete(DeleteBehavior.Cascade)  // Remove wishlist item when wishlist deleted
                );

            // ── One-to-One: Order ↔ Transaction ──
            // Each order has exactly one payment transaction for financial tracking
            // Business rule: Transaction created atomically with order completion
            modelBuilder.Entity<OrderModel>()
                .HasOne(o => o.Transaction)
                .WithOne(t => t.Order)
                .HasForeignKey<TransactionModel>(t => t.FkOrderId)
                .OnDelete(DeleteBehavior.Cascade); // Remove transaction when order is deleted

            modelBuilder.Entity<OrderModel>()
                .HasOne(o => o.RegisteredUser)
                .WithMany(u => u.Orders)
                .HasForeignKey(o => o.FkRegisteredUserId)
                .OnDelete(DeleteBehavior.SetNull);

            #endregion

            #region Performance Optimization Indexes

            // ── Product Search Performance ──
            // Normalized product names enable fast fuzzy search and autocomplete functionality
            // Index supports LIKE queries and full-text search operations
            modelBuilder.Entity<ProductModel>()
                .HasIndex(p => p.NameNormalized)
                .HasDatabaseName("IX_Products_NameNormalized");

            // ── Fuzzy Search Performance ──
            // Normalized suggestion names for rapid autocomplete response times
            // Critical for real-time search suggestions during user typing
            modelBuilder.Entity<FuzzySuggestionModel>()
                .HasIndex(f => f.NameNormalized)
                .HasDatabaseName("IX_FuzzySuggestions_NameNormalized");

            modelBuilder.Entity<OrderModel>()
                .HasIndex(o => o.GuestAccessTokenHash)
                .IsUnique()
                .HasDatabaseName("IX_Orders_GuestAccessTokenHash");

            #endregion

            #region Infrastructure Table Mapping

            // ── Audit and Compliance Infrastructure ──
            // Custom table names for clarity and separation from business entities
            modelBuilder.Entity<AuditEntryModel>().ToTable("AuditEntries");
            modelBuilder.Entity<CachedFuzzyKeyModel>().ToTable("CachedFuzzyKeys");
            modelBuilder.Entity<FuzzySuggestionModel>().ToTable("FuzzySuggestions");

            #endregion

            #region Search System Relationships

            // ── Fuzzy Search Product Linkage ──
            // Links fuzzy suggestions back to source products for result accuracy
            // Enables suggestion relevance scoring and product data enrichment
            modelBuilder.Entity<FuzzySuggestionModel>()
                .HasOne<ProductModel>()
                .WithMany(p => p.FuzzySuggestions)
                .HasForeignKey(f => f.PkProductId)
                .OnDelete(DeleteBehavior.Cascade); // Remove suggestions when product deleted

            #endregion

            #region Additional Relationship Constraints

            // ── User Activity Tracking ──
            // Ensure user logs are properly linked for activity monitoring and security
            modelBuilder.Entity<UserLogModel>()
                .HasOne<RegisteredUserModel>()
                .WithMany()
                .HasForeignKey("UserId")
                .OnDelete(DeleteBehavior.Cascade);

            // ── Order Item Product References ──
            // Maintain referential integrity between order items and products
            // Soft delete approach: Orders preserve product information even if product is removed
            modelBuilder.Entity<OrderItemModel>()
                .HasOne(oi => oi.Product)
                .WithMany()
                .HasForeignKey(oi => oi.FkProductId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent product deletion if ordered

            // ── Coupon System Relationships ──
            // Configure coupon-order many-to-many relationship via junction table
            modelBuilder.Entity<OrderCouponModel>()
                .HasOne(oc => oc.Order)
                .WithMany(o => o.OrderCoupons)
                .HasForeignKey(oc => oc.FkOrderId)
                .OnDelete(DeleteBehavior.Cascade); // Remove coupon usage when order deleted

            modelBuilder.Entity<OrderCouponModel>()
                .HasOne(oc => oc.Coupon)
                .WithMany(c => c.OrderCoupons)
                .HasForeignKey(oc => oc.FkCouponId)
                .OnDelete(DeleteBehavior.Restrict); // Preserve coupon records for audit

            // ── Coupon Code Uniqueness ──
            // Ensure coupon codes are unique for proper validation
            modelBuilder.Entity<CouponModel>()
                .HasIndex(c => c.Code)
                .IsUnique()
                .HasDatabaseName("IX_Coupons_Code_Unique");

            modelBuilder.Entity<TransactionModel>()
                .HasIndex(t => t.PaymentOrderId)
                .IsUnique()
                .HasDatabaseName("IX_Transactions_PaymentOrderId_Unique");

            modelBuilder.Entity<TransactionModel>()
                .HasIndex(t => t.PaymentTransactionId)
                .IsUnique()
                .HasDatabaseName("IX_Transactions_PaymentTransactionId_Unique");

            #endregion
        }

        #endregion
    }
}
