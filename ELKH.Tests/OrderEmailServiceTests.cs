using System.Linq;
using System.Threading.Tasks;
using ELKH.Services;
using Moq;
using Xunit;

namespace ELKH.Tests;

/// <summary>
/// Unit tests for <see cref="OrderEmailService"/>.
/// Verifies that each method delegates to <see cref="IEmailSender"/> with
/// the expected recipient, a non-empty subject, and an HTML body.
/// </summary>
public class OrderEmailServiceTests
{
    private static (OrderEmailService svc, Mock<IEmailSender> sender) Build()
    {
        var sender = new Mock<IEmailSender>();
        sender.Setup(s => s.SendEmailAsync(
                It.IsAny<string[]>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
              .Returns(Task.CompletedTask);
        return (new OrderEmailService(sender.Object), sender);
    }

    [Fact]
    public async Task SendOrderConfirmationAsync_CallsSenderWithCorrectRecipient()
    {
        var (svc, sender) = Build();
        await svc.SendOrderConfirmationAsync("jane@test.com", "Jane", 42);

        sender.Verify(s => s.SendEmailAsync(
            It.Is<string[]>(to => to.Contains("jane@test.com")),
            It.Is<string>(sub => sub.Contains("42")),
            It.Is<string>(body => body.Contains("Jane") && body.Contains("42")),
            It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task SendShippedAsync_CallsSenderWithCorrectRecipient()
    {
        var (svc, sender) = Build();
        await svc.SendShippedAsync("bob@test.com", "Bob", 7);

        sender.Verify(s => s.SendEmailAsync(
            It.Is<string[]>(to => to.Contains("bob@test.com")),
            It.Is<string>(sub => sub.Contains("7") && sub.Contains("shipped")),
            It.Is<string>(body => body.Contains("Bob")),
            It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task SendDeliveredAsync_CallsSenderWithCorrectRecipient()
    {
        var (svc, sender) = Build();
        await svc.SendDeliveredAsync("alice@test.com", "Alice", 15);

        sender.Verify(s => s.SendEmailAsync(
            It.Is<string[]>(to => to.Contains("alice@test.com")),
            It.Is<string>(sub => sub.Contains("15") && sub.Contains("delivered")),
            It.Is<string>(body => body.Contains("Alice")),
            It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task SendOrderConfirmationAsync_SubjectContainsOrderId()
    {
        var (svc, sender) = Build();
        await svc.SendOrderConfirmationAsync("x@test.com", "X", 999);

        sender.Verify(s => s.SendEmailAsync(
            It.IsAny<string[]>(),
            It.Is<string>(sub => sub.Contains("999")),
            It.IsAny<string>(),
            It.IsAny<string>()),
            Times.Once);
    }
}
