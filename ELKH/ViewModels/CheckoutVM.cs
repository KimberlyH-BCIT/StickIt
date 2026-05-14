using System.ComponentModel.DataAnnotations;
using ELKH.Models;

namespace ELKH.ViewModels;

/// <summary>
/// View model for checkout process containing order information, payment details,
/// shipping addresses, and customer data for order completion workflow.
/// </summary>
public class CheckoutVM
{
    // PayPal order ID (created client-side, captured server-side)
    [Required(ErrorMessage = "Please complete PayPal payment before placing your order")]
    public string? PayPalOrderId { get; set; }

    public string? PayPalPayerId { get; set; }

    // Selected contact detail ID (for existing addresses)
    public int? SelectedContactId { get; set; }

    // Available saved addresses for the user
    public List<ContactDetailVM> SavedAddresses { get; set; } = new();

    // Shipping method selection
    [Required(ErrorMessage = "Please select a shipping method")]
    [Display(Name = "Shipping Method")]
    public int SelectedShippingMethodId { get; set; }

    // Available shipping methods for selection
    public List<ShippingMethodModel> AvailableShippingMethods { get; set; } = new();

    // Shipping info (populated from selected address or entered manually)
    [Required, Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required, Display(Name = "Street Address")]
    public string Street { get; set; } = string.Empty;

    [Required]
    public string City { get; set; } = string.Empty;

    [Required]
    public string Province { get; set; } = string.Empty;

    [Required, Display(Name = "Postal Code")]
    public string PostalCode { get; set; } = string.Empty;

    [Required]
    public string Country { get; set; } = "Canada";

    [Required, Display(Name = "Phone Number")]
    public string PhoneNumber { get; set; } = string.Empty;

    // Order summary 
    public List<CartItemVM> Items { get; set; } = new();
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal Total { get; set; }
}
