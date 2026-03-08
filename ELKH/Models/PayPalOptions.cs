namespace ELKH.Models;

public class PayPalOptions
{
    public string ClientId { get; set; } = "";
    public string Secret { get; set; } = "";
    public string Environment { get; set; } = "sandbox"; // sandbox | live
    public string Currency { get; set; } = "CAD";
}