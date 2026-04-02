namespace ELKH.ViewModels
{
    /// <summary>View model for the customer dashboard.</summary>
    public class UserDashboardVM
    {
        /// <summary>User's profile information.</summary>
        public UserProfileVM? Profile { get; set; }

        /// <summary>Total wishlist items (always unfiltered) - used for the header badge.</summary>
        public int WishlistCount { get; set; }

        /// <summary>Paginated active orders section (initial page 1 load).</summary>
        public OrderSectionVM ActiveOrdersSection { get; set; } = new();

        /// <summary>Paginated wishlist section (initial page 1 load).</summary>
        public WishlistSectionVM WishlistSection { get; set; } = new();

        /// <summary>Paginated order history section (initial page 1 load).</summary>
        public OrderSectionVM OrderHistorySection { get; set; } = new();
    }

    /// <summary>Lightweight order summary for dashboard sections.</summary>
    public class DashboardOrderVM
    {
        public int OrderId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string OrderStatus { get; set; } = string.Empty;
        public string DeliveryStatus { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public int ItemCount { get; set; }
    }

    /// <summary>Lightweight wishlist item for the dashboard.</summary>
    public class WishlistPreviewItemVM
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal DiscountPercent { get; set; }
        public decimal EffectivePrice => DiscountPercent > 0 ? Price * (1 - DiscountPercent / 100) : Price;
    }

    /// <summary>Paginated wishlist section result.</summary>
    public class WishlistSectionVM
    {
        public List<WishlistPreviewItemVM> Items { get; set; } = [];
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
        public int TotalItems { get; set; }
        public string CurrentSort { get; set; } = "date_desc";
    }

    /// <summary>Paginated order section result - shared by active orders and order history.</summary>
    public class OrderSectionVM
    {
        public List<DashboardOrderVM> Items { get; set; } = [];
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
        public int TotalItems { get; set; }
        public string CurrentSort { get; set; } = "date_desc";
        public bool IsActiveSection { get; set; }
    }

    /// <summary>
    /// Bundled return value for <see cref="ELKH.Services.IUserService.GetDashboardDataAsync"/>.
    /// Groups all four dashboard queries so the controller makes a single service call
    /// instead of four sequential awaits.
    /// </summary>
    public record DashboardData(
        int WishlistCount,
        WishlistSectionVM Wishlist,
        OrderSectionVM ActiveOrders,
        OrderSectionVM OrderHistory);
}
