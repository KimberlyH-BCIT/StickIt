using System.ComponentModel.DataAnnotations;
using ELKH.Models;

namespace ELKH.ViewModels
{
    /// <summary>
    /// ViewModel for guest checkout process - collects shipping and contact information
    /// without requiring account creation.
    /// </summary>
    /// <remarks>
    /// TABLE OF CONTENTS
    /// ================================================================================
    /// 1. Contact Information ......................................... Lines [25-49]
    ///    - Email                         // Order confirmation and tracking
    ///    - FullName                      // Shipping label name
    ///    - PhoneNumber                   // Delivery contact
    /// 
    /// 2. Shipping Address ............................................ Lines [51-88]
    ///    - Street                        // Complete street address
    ///    - City, Province                // Geographic location
    ///    - PostalCode                    // Canadian postal validation
    ///    - Country                       // Default: Canada
    /// 
    /// 3. Shipping Method Selection ................................... Lines [90-100]
    ///    - SelectedShippingMethodId      // Chosen delivery option
    ///    - AvailableShippingMethods      // List of shipping options
    /// 
    /// 4. Optional Features ........................................... Lines [102-128]
    ///    - SubscribeToNewsletter         // Marketing opt-in
    ///    - CreateAccount                 // Post-checkout account creation
    ///    - Password validation           // Account creation security
    /// 
    /// 5. Order Summary & Calculation ................................. Lines [130-159]
    ///    - Items, Subtotal               // Cart contents and pricing
    ///    - Tax, ShippingCost             // BC tax and delivery fees
    ///    - Total                         // Final order amount
    ///    - PayPalClientId                // Payment integration
    /// ================================================================================
    /// 
    /// ARCHITECTURAL CONTEXT:
    /// • Core ViewModel for ELKH's guest checkout workflow
    /// • Enables purchase without mandatory account creation
    /// • Implements comprehensive Canadian address validation
    /// • Integrates with PayPal payment processing
    /// • Supports optional account creation post-purchase
    /// 
    /// BUSINESS LOGIC & FEATURES:
    /// • Streamlined guest checkout reducing cart abandonment
    /// • Canadian shipping focus with postal code validation
    /// • BC tax calculation (12% composite rate)
    /// • Free shipping threshold ($50+ orders)
    /// • Optional newsletter subscription and account creation
    /// • Order tracking via email without account requirement
    /// 
    /// VALIDATION & SECURITY:
    /// • Comprehensive input validation with error messages
    /// • Canadian postal code regex validation (A1A 1A1 format)
    /// • North American phone number validation
    /// • Email format validation for order communications
    /// • Password complexity requirements for optional accounts
    /// • CSRF protection through model binding
    /// 
    /// INTEGRATION POINTS:
    /// • Used by: CheckoutController for guest purchase workflow
    /// • Integrates with: ShippingMethodModel for delivery options
    /// • Connects to: CartItemVM for order summary display
    /// • Payment: PayPal SDK integration via ClientId
    /// • Post-purchase: Optional account creation and order tracking
    /// 
    /// USER EXPERIENCE FEATURES:
    /// • Single-page checkout form with live validation
    /// • Real-time order total calculation
    /// • Progressive disclosure (account fields only if requested)
    /// • Clear shipping cost communication
    /// • Newsletter opt-in without aggressive marketing
    /// </remarks>
    public class GuestCheckoutVM
    {
        /// <summary>
        /// PayPal order ID returned by the browser flow and verified on the server.
        /// </summary>
        [Required(ErrorMessage = "Please complete PayPal payment before placing your order")]
        public string? PayPalOrderId { get; set; }

        /// <summary>
        /// Optional payer ID returned by the browser for correlation only.
        /// </summary>
        public string? PayPalPayerId { get; set; }

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
