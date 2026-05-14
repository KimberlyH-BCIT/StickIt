namespace ELKH.Services;

/// <summary>
/// Sends transactional order-status emails to customers.
/// Decoupled from the transport layer via <see cref="IEmailSender"/>.
/// </summary>
public interface IOrderEmailService
{
    Task SendOrderConfirmationAsync(string customerEmail, string customerFirstName, int orderId, string? confirmationLink = null);
    Task SendShippedAsync(string customerEmail, string customerFirstName, int orderId);
    Task SendDeliveredAsync(string customerEmail, string customerFirstName, int orderId);
}
