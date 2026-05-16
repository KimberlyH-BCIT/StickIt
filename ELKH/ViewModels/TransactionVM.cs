using System.ComponentModel.DataAnnotations;
using ELKH.Models;

namespace ELKH.ViewModels;

/// <summary>
/// View model for transaction information containing payment details,
/// transaction status, and financial data for transaction management and reporting.
/// </summary>
public class TransactionVM
{
    public string FirstName { get; set; } = string.Empty;
    public int PkTransactionId { get; set; }
    [Display(Name = "Transaction Status")]
    public string TransactionStatus { get; set; } = string.Empty;
    [DisplayFormat(DataFormatString = "{0:F2}")]
    public decimal Amount { get; set; } = 0;
    [Display(Name = "Transaction Time")]
    public DateTime TransactionDate { get; set; } = DateTime.Now;
    [Display(Name = "Delivery Fee")]
    public decimal DeliveryFee { get; set; } = 0;

    //Relationship with Order
    public int FkOrderId { get; set; }
    public OrderModel Order { get; set; } = new OrderModel();
}
