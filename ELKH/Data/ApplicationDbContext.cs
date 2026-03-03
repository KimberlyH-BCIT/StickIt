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
        public DbSet<ProductImageModel> ProductImages { get; set; }
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

            modelBuilder.Entity<OrderModel>()
                .HasOne(o => o.OrderStatuses)
                .WithOne(d => d.Order)
                .HasForeignKey<OrderStatusModel>(o => o.FkOrderId);

            modelBuilder.Entity<RegisteredUserModel>()
                        .HasOne(r => r.WishLists)
                        .WithOne(w => w.RegisteredUser)
                        .HasForeignKey<WishListModel>(w => w.FkUserId);

            modelBuilder.Entity<OrderModel>()
                        .HasOne(o => o.Transaction)
                        .WithOne(t => t.Order)
                        .HasForeignKey<TransactionModel>(t => t.FkOrderId);

        }
    }

}
