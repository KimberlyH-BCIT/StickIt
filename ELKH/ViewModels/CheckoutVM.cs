using System.ComponentModel.DataAnnotations;

namespace ELKH.ViewModels;

public class CheckoutVM
{
    // Shipping info
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