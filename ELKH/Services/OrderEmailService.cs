namespace ELKH.Services;

/// <inheritdoc />
public class OrderEmailService : IOrderEmailService
{
    private readonly IEmailSender _email;

    public OrderEmailService(IEmailSender email) => _email = email;

    /// <inheritdoc />
    public Task SendShippedAsync(string customerEmail, string customerFirstName, int orderId)
    {
        var subject = $"Your order #{orderId} has shipped! 📦";
        var body = $"""
            <p>Hi {customerFirstName},</p>
            <p>Great news — <strong>Order #{orderId}</strong> has been shipped and is on its way to you.</p>
            <p>You'll get another email once it's delivered.</p>
            <p>Thanks for shopping with ELKH! 🎉</p>
            """;
        return _email.SendEmailAsync([customerEmail], subject, body);
    }

    /// <inheritdoc />
    public Task SendDeliveredAsync(string customerEmail, string customerFirstName, int orderId)
    {
        var subject = $"Your order #{orderId} has been delivered! 🎉";
        var body = $"""
            <p>Hi {customerFirstName},</p>
            <p><strong>Order #{orderId}</strong> has been delivered — we hope you love your stickers!</p>
            <p>Got a minute? Head to your order history to leave a review and help other shoppers.</p>
            <p>Thanks for shopping with ELKH!</p>
            """;
        return _email.SendEmailAsync([customerEmail], subject, body);
    }
}
