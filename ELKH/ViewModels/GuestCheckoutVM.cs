using System.ComponentModel.DataAnnotations;
using ELKH.Models;

namespace ELKH.ViewModels
{
    /// <summary>
    /// ViewModel for guest checkout process - collects shipping and contact information
    /// without requiring account creation.
    /// </summary>
    /// <remarks>
    /// Used when anonymous users checkout without creating an account.
    /// After successful order, guest can optionally create an account to track order.
    /// 
    /// FIELDS COLLECTED:
    /// - Email (required for order confirmation and tracking)
    /// - Full name (shipping label)
    /// - Phone number (delivery contact)
    /// - Complete shipping address
    /// - Shipping method selection
    /// - Optional: Newsletter subscription
    /// - Optional: Create account after checkout
    /// </remarks>
    public class GuestCheckoutVM
    {
        /// <summary>
        /// Guest email address - used for order confirmation and tracking link
        /// </summary>
        [Required(ErrorMessage = "Email address is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Guest full name for shipping label
        /// </summary>
        [Required(ErrorMessage = "Full name is required")]
        [Display(Name = "Full Name")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        public string FullName { get; set; } = string.Empty;

        /// <summary>
        /// Contact phone number for delivery
        /// </summary>
        [Required(ErrorMessage = "Phone number is required")]
        [Phone(ErrorMessage = "Please enter a valid phone number")]
        [Display(Name = "Phone Number")]
        [RegularExpression(@"^\+?1?\s*\(?([0-9]{3})\)?[-.\s]?([0-9]{3})[-.\s]?([0-9]{4})$", 
            ErrorMessage = "Please enter a valid North American phone number (e.g., (604) 555-1234)")]
        public string PhoneNumber { get; set; } = string.Empty;

        /// <summary>
        /// Street address including unit/apartment number
        /// </summary>
        [Required(ErrorMessage = "Street address is required")]
        [Display(Name = "Street Address")]
        [StringLength(200, ErrorMessage = "Address cannot exceed 200 characters")]
        public string Street { get; set; } = string.Empty;

        /// <summary>
        /// City name
        /// </summary>
        [Required(ErrorMessage = "City is required")]
        [Display(Name = "City")]
        [StringLength(100, ErrorMessage = "City name cannot exceed 100 characters")]
        public string City { get; set; } = string.Empty;

        /// <summary>
        /// Province/State (Canadian provinces for shipping)
        /// </summary>
        [Required(ErrorMessage = "Province is required")]
        [Display(Name = "Province")]
        public string Province { get; set; } = string.Empty;

        /// <summary>
        /// Postal code (Canadian format: A1A 1A1)
        /// </summary>
        [Required(ErrorMessage = "Postal code is required")]
        [Display(Name = "Postal Code")]
        [RegularExpression(@"^[A-Za-z]\d[A-Za-z][ -]?\d[A-Za-z]\d$", 
            ErrorMessage = "Please enter a valid Canadian postal code (e.g., V6B 1A1)")]
        public string PostalCode { get; set; } = string.Empty;

        /// <summary>
        /// Country (default: Canada)
        /// </summary>
        [Required(ErrorMessage = "Country is required")]
        [Display(Name = "Country")]
        public string Country { get; set; } = "Canada";

        /// <summary>
        /// Selected shipping method ID
        /// </summary>
        [Required(ErrorMessage = "Please select a shipping method")]
        [Display(Name = "Shipping Method")]
        public int SelectedShippingMethodId { get; set; }

        /// <summary>
        /// Available shipping methods for selection
        /// </summary>
        public List<ShippingMethodModel> AvailableShippingMethods { get; set; } = new();

        /// <summary>
        /// Optional: Guest wants to subscribe to newsletter
        /// </summary>
        [Display(Name = "Send me updates about new products and exclusive offers")]
        public bool SubscribeToNewsletter { get; set; } = false;

        /// <summary>
        /// Optional: Guest wants to create account after checkout
        /// </summary>
        [Display(Name = "Create an account to track my order and save my information")]
        public bool CreateAccount { get; set; } = false;

        /// <summary>
        /// Password for account creation (only if CreateAccount is true)
        /// </summary>
        [Display(Name = "Password")]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters long")]
        public string? Password { get; set; }

        /// <summary>
        /// Confirm password for account creation
        /// </summary>
        [Display(Name = "Confirm Password")]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        public string? ConfirmPassword { get; set; }

        /// <summary>
        /// Cart items for order summary display
        /// </summary>
        public List<CartItemVM> Items { get; set; } = new();

        /// <summary>
        /// Calculated subtotal before tax and shipping
        /// </summary>
        public decimal Subtotal => Items.Sum(i => i.LineTotal);

        /// <summary>
        /// Tax amount (12% BC composite rate)
        /// </summary>
        public decimal Tax { get; set; }

        /// <summary>
        /// Shipping cost ($5.99 or free over $50)
        /// </summary>
        public decimal ShippingCost { get; set; }

        /// <summary>
        /// Grand total including tax and shipping
        /// </summary>
        public decimal Total => Subtotal + Tax + ShippingCost;

        /// <summary>
        /// PayPal client ID for payment button
        /// </summary>
        public string? PayPalClientId { get; set; }
    }
}
