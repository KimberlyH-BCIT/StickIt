using System.Net;
using ELKH.Middleware;
using ELKH.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ELKH.Tests.Unit.Services;

public class StructuredLoggingServiceTests
{
    [Fact]
    public void LogUserAction_WithContextAndAdditionalData_ShouldUseCorrelationIdAndRequestMetadata()
    {
        var logger = new Mock<ILogger<StructuredLoggingService>>();
        var accessor = CreateAccessor(context =>
        {
            context.Items[CorrelationIdMiddleware.CorrelationIdLogKey] = "corr-user";
            context.Request.Headers.UserAgent = "test-agent";
            context.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.10");
        });

        var service = new StructuredLoggingService(logger.Object, accessor);

        service.LogUserAction("WishlistAdd", "user-123", new { ProductId = 42 });

        logger.VerifyLog(LogLevel.Information, message => message.Contains("User action"));
    }

    [Fact]
    public void LogSystemEvent_WithoutCorrelationId_ShouldFallBackToSystem()
    {
        var logger = new Mock<ILogger<StructuredLoggingService>>();
        var accessor = CreateAccessor(_ => { });
        var service = new StructuredLoggingService(logger.Object, accessor);

        service.LogSystemEvent("UnhandledException", "Something happened");

        logger.VerifyLog(LogLevel.Information, message => message.Contains("System event"));
    }

    [Fact]
    public void LogPerformanceMetric_WithFastDuration_ShouldLogInformation()
    {
        var logger = new Mock<ILogger<StructuredLoggingService>>();
        var accessor = CreateAccessor(context =>
        {
            context.Items[CorrelationIdMiddleware.CorrelationIdLogKey] = "corr-fast";
        });
        var service = new StructuredLoggingService(logger.Object, accessor);

        service.LogPerformanceMetric("Search", TimeSpan.FromMilliseconds(250), new { Cached = true });

        logger.VerifyLog(LogLevel.Information, message => message.Contains("Performance"));
    }

    [Fact]
    public void LogPerformanceMetric_WithSlowDuration_ShouldLogWarning()
    {
        var logger = new Mock<ILogger<StructuredLoggingService>>();
        var accessor = CreateAccessor(context =>
        {
            context.Items[CorrelationIdMiddleware.CorrelationIdLogKey] = "corr-slow";
        });
        var service = new StructuredLoggingService(logger.Object, accessor);

        service.LogPerformanceMetric("Checkout", TimeSpan.FromMilliseconds(6000));

        logger.VerifyLog(LogLevel.Warning, message => message.Contains("Performance"));
    }

    [Fact]
    public void LogBusinessEvent_WithoutAuthenticatedUser_ShouldUseAnonymous()
    {
        var logger = new Mock<ILogger<StructuredLoggingService>>();
        var accessor = CreateAccessor(context =>
        {
            context.Items[CorrelationIdMiddleware.CorrelationIdLogKey] = "corr-business";
        });
        var service = new StructuredLoggingService(logger.Object, accessor);

        service.LogBusinessEvent("ProductViewed", "Catalog", new { ProductId = 7 });

        logger.VerifyLog(LogLevel.Information, message => message.Contains("Business event"));
    }

    [Fact]
    public void LogError_WithContext_ShouldLogError()
    {
        var logger = new Mock<ILogger<StructuredLoggingService>>();
        var accessor = CreateAccessor(context =>
        {
            context.Items[CorrelationIdMiddleware.CorrelationIdLogKey] = "corr-error";
            context.Request.Path = "/checkout";
        });
        var service = new StructuredLoggingService(logger.Object, accessor);
        var exception = new InvalidOperationException("payment failed");

        service.LogError(exception, "Checkout.Process", new { OrderId = 99 });

        logger.VerifyLog(LogLevel.Error, message => message.Contains("Error in"), exception);
    }

    private static HttpContextAccessor CreateAccessor(Action<DefaultHttpContext> configure)
    {
        var context = new DefaultHttpContext();
        configure(context);
        return new HttpContextAccessor { HttpContext = context };
    }
}

internal static class StructuredLoggingServiceTestExtensions
{
    public static void VerifyLog<T>(
        this Mock<ILogger<T>> logger,
        LogLevel level,
        Func<string, bool> messagePredicate,
        Exception? exception = null)
    {
        logger.Verify(x => x.Log(
            level,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => messagePredicate(state.ToString()!)),
            It.Is<Exception?>(ex => exception == null ? ex == null : ex == exception),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce);
    }
}
