using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ELKH.Models;

namespace ELKH.Data
{ 
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<ProductModel> Products { get; set; }
        public DbSet<CategoryModel> Categories { get; set; }
        public DbSet<ImageModel> ProductImages { get; set; }
        public DbSet<RegisteredUserModel> RegisteredUsers { get; set; }
        public DbSet<CartModel> Carts { get; set; }
        public DbSet<OrderModel> Orders { get; set; }
        public DbSet<OrderItemModel> OrderItems { get; set; }
        public DbSet<TransactionModel> Transactions { get; set; }
        public DbSet<ContactDetailModel> ContactDetails { get; set; }
        public DbSet<OrderStatusModel> OrderStatuses { get; set; }
        public DbSet<ProductRatingModel> ProductRatings { get; set; }
        public DbSet<WishListModel> WishLists { get; set; }
        public DbSet<UserLogModel> UserLogs { get; set; }
        public DbSet<UserProfileModel> UserProfiles { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // NOTE: The one-to-one configuration for Order <-> OrderStatus caused design-time
            // errors when creating the ImageStoreContext. If you need this relationship,
            // re-add configuration ensuring the dependent side is explicit and navigation
            // properties are nullable (no default instantiation in model classes).
            modelBuilder.Entity<OrderStatusModel>()
                .HasOne(os => os.Order)
                .WithOne(o => o.OrderStatuses)
                .HasForeignKey<OrderStatusModel>(os => os.FkOrderId);

            modelBuilder.Entity<RegisteredUserModel>()
                        .HasOne(r => r.WishLists)
                        .WithOne(w => w.RegisteredUser)
                        .HasForeignKey<WishListModel>(w => w.FkUserId);

            modelBuilder.Entity<OrderModel>()
                        .HasOne(o => o.Transaction)
                        .WithOne(t => t.Order)
                        .HasForeignKey<TransactionModel>(t => t.FkOrderId);


            modelBuilder.Entity<ProductModel>()
                .HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.FkCategoryId);
            
            modelBuilder.Entity<ProductModel>()
                .HasOne(p => p.WishList)
                .WithMany(w => w.Products)
                .HasForeignKey(p => p.FkWishListId);

            modelBuilder.Entity<ProductModel>().HasData(
                    new ProductModel
                    {
                        PkProductId = 1,
                        Name = "Pikacu",
                        Description = "Character from anime",
                        Price = 2.99m,
                        StockQuantity = 10,
                        IsActive = true,
                        FkCategoryId = 1
                    },
                    new ProductModel
                    {
                        PkProductId = 2,
                        Name = "Random",
                        Description = "Random",
                        Price = 1.99m,
                        StockQuantity = 80,
                        IsActive = true,
                        FkCategoryId = 1
                    },
                    new ProductModel
                    {
                        PkProductId = 3,
                        Name = "Random2",
                        Description = "Random2",
                        Price = 4.99m,
                        StockQuantity = 80,
                        IsActive = true,
                        FkCategoryId = 2
                    }
                );
            modelBuilder.Entity<CategoryModel>().HasData(
                    new CategoryModel
                    {
                        PkCategoryId = 1,
                        CategoryName = "Carton"
                    },
                    new CategoryModel
                    {
                        PkCategoryId = 2,
                        CategoryName = "Fake"
                    }
                );
        }

    }

}
