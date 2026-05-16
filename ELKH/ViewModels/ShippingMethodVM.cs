using System.ComponentModel.DataAnnotations;

namespace ELKH.ViewModels
{
    /// <summary>
    /// View model for shipping method management in the Manager interface.
    /// Provides form binding and validation for shipping method CRUD operations.
    /// </summary>
    /// <remarks>
    /// PURPOSE:
    /// - Form data binding for shipping method creation and editing
    /// - Client-side and server-side validation rules
    /// - Data transfer between controller and views
    /// - Business rule enforcement through data annotations
    /// 
    /// VALIDATION RULES:
    /// - Name: Required, length limits, uniqueness (validated in controller)
    /// - Description: Optional with length limit
    /// - BasePrice: Required, non-negative, currency format
    /// - Delivery days: Required, positive integers, logical min ≤ max
    /// - Display order: Required, positive integer for sorting
    /// 
    /// FORM FEATURES:
    /// - Pre-population for edit scenarios
    /// - Validation error display
    /// - Bootstrap-compatible styling attributes
    /// - Accessibility labels and descriptions
    /// </remarks>
    public class ShippingMethodVM
    {
        /// <summary>
        /// Primary key for edit operations. Zero for create operations.
        /// </summary>
        public int PkShippingMethodId { get; set; }

        /// <summary>
        /// Shipping method name displayed to customers during checkout.
        /// Must be unique across all shipping methods.
        /// </summary>
        [Required(ErrorMessage = "Shipping method name is required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 100 characters")]
        [Display(Name = "Method Name")]
        public string Name { get; set; } = "";

        /// <summary>
        /// Optional description providing additional details about delivery service.
        /// Displayed to customers to help choose appropriate shipping option.
        /// </summary>
        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        /// <summary>
        /// Base shipping price before any free shipping threshold calculations.
        /// Displayed as the standard cost for this shipping method.
        /// </summary>
        [Required(ErrorMessage = "Base price is required")]
        [Range(0.00, 999.99, ErrorMessage = "Base price must be between $0.00 and $999.99")]
        [Display(Name = "Base Price")]
        [DisplayFormat(DataFormatString = "{0:C}", ApplyFormatInEditMode = false)]
        public decimal BasePrice { get; set; }

        /// <summary>
        /// Minimum delivery timeframe in business days.
        /// Used to set customer expectations for delivery timing.
        /// </summary>
        [Required(ErrorMessage = "Minimum delivery days is required")]
        [Range(1, 30, ErrorMessage = "Minimum delivery days must be between 1 and 30")]
        [Display(Name = "Minimum Delivery Days")]
        public int DeliveryDaysMin { get; set; }

        /// <summary>
        /// Maximum delivery timeframe in business days.
        /// Must be greater than or equal to minimum delivery days.
        /// </summary>
        [Required(ErrorMessage = "Maximum delivery days is required")]
        [Range(1, 30, ErrorMessage = "Maximum delivery days must be between 1 and 30")]
        [Display(Name = "Maximum Delivery Days")]
        public int DeliveryDaysMax { get; set; }

        /// <summary>
        /// Whether this shipping method is available for customer selection.
        /// Inactive methods are hidden from checkout but preserved for historical orders.
        /// </summary>
        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Determines the order in which shipping methods appear during checkout.
        /// Lower numbers appear first in the customer interface.
        /// </summary>
        [Required(ErrorMessage = "Display order is required")]
        [Range(1, 999, ErrorMessage = "Display order must be between 1 and 999")]
        [Display(Name = "Display Order")]
        public int DisplayOrder { get; set; }

        /// <summary>
        /// Timestamp when the shipping method was originally created.
        /// Set automatically on creation, preserved during updates.
        /// </summary>
        [Display(Name = "Created At")]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Timestamp when the shipping method was last updated.
        /// Updated automatically on each save operation.
        /// </summary>
        [Display(Name = "Updated At")]
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// Computed property for displaying delivery timeframe in a user-friendly format.
        /// Returns formatted string like "5-7 business days" or "1 business day".
        /// </summary>
        public string DeliveryTimeframe
        {
            get
            {
                if (DeliveryDaysMin == DeliveryDaysMax)
                {
                    return DeliveryDaysMin == 1
                        ? "1 business day"
                        : $"{DeliveryDaysMin} business days";
                }
                return $"{DeliveryDaysMin}-{DeliveryDaysMax} business days";
            }
        }

        /// <summary>
        /// Computed property for displaying active status in a user-friendly format.
        /// Returns "Active" or "Inactive" for display in tables and forms.
        /// </summary>
        public string StatusDisplay => IsActive ? "Active" : "Inactive";

        /// <summary>
        /// Bootstrap CSS class for status badge styling.
        /// Returns appropriate badge class for visual status indication.
        /// </summary>
        public string StatusBadgeClass => IsActive ? "badge-success" : "badge-secondary";
    }
}
