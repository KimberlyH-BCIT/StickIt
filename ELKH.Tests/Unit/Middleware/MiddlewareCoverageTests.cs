using System.IO;
using System.Net;
using System.Security;
using System.Text.Json;
using ELKH.Middleware;
using ELKH.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ELKH.Tests.Unit.Middleware;

// TABLE OF CONTENTS
// - Correlation ID middleware tests
// - Correlation ID extension tests
// - Global exception middleware tests

/// <summary>
/// Unit tests covering correlation ID and global exception middleware behavior.
/// </summary>
/// <remarks>
/// 1. Correlation ID middleware tests
/// 2. Correlation ID extension tests
/// 3. Global exception middleware tests
/// </remarks>
public class MiddlewareCoverageTests
{
    [Fact]
    public async Task CorrelationIdMiddleware_WithExistingHeader_ShouldReuseAndExposeCorrelationId()
    {
        var logger = new Mock<ILogger<CorrelationIdMiddleware>>();
        var middleware = new CorrelationIdMiddleware(context =>
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        }, logger.Object);

        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.CorrelationIdHeader] = "corr-123";
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/products";
        context.Request.Headers.UserAgent = "unit-test-agent";

        await middleware.InvokeAsync(context);

        context.Response.Headers[CorrelationIdMiddleware.CorrelationIdHeader].ToString().Should().Be("corr-123");
        context.Items[CorrelationIdMiddleware.CorrelationIdLogKey].Should().Be("corr-123");
        context.GetCorrelationId().Should().Be("corr-123");

        var accessor = new HttpContextAccessor { HttpContext = context };
        accessor.GetCorrelationId().Should().Be("corr-123");
    }

    [Fact]
    public async Task CorrelationIdMiddleware_WithoutHeader_ShouldGenerateCorrelationId()
    {
        var logger = new Mock<ILogger<CorrelationIdMiddleware>>();
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask, logger.Object);
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/checkout";

        await middleware.InvokeAsync(context);

        var correlationId = context.Response.Headers[CorrelationIdMiddleware.CorrelationIdHeader].ToString();
        correlationId.Should().NotBeNullOrWhiteSpace();
        context.Items[CorrelationIdMiddleware.CorrelationIdLogKey].Should().Be(correlationId);
    }

    [Fact]
    public async Task CorrelationIdMiddleware_WhenNextThrows_ShouldPreserveCorrelationIdAndRethrow()
    {
        var logger = new Mock<ILogger<CorrelationIdMiddleware>>();
        var middleware = new CorrelationIdMiddleware(_ => throw new InvalidOperationException("boom"), logger.Object);
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.CorrelationIdHeader] = "corr-error";
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/failing";

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));

        context.Response.Headers[CorrelationIdMiddleware.CorrelationIdHeader].ToString().Should().Be("corr-error");
        context.GetCorrelationId().Should().Be("corr-error");
    }

    [Fact]
    public void CorrelationIdExtensions_WithMissingHttpContext_ShouldReturnNull()
    {
        var accessor = new HttpContextAccessor();

        accessor.GetCorrelationId().Should().BeNull();
    }

    [Fact]
    public async Task GlobalExceptionMiddleware_WithUnauthorizedAccessException_ShouldReturnUnauthorizedResponse()
    {
        var structuredLogger = new Mock<IStructuredLoggingService>();
        var middleware = CreateGlobalExceptionMiddleware(
            _ => throw new UnauthorizedAccessException(),
            structuredLogger,
            Environments.Production);

        var context = CreateHttpContext(structuredLogger.Object, "/api/orders", HttpMethods.Get);
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");

        await middleware.InvokeAsync(context);

        var response = await ReadErrorResponseAsync(context);
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.Unauthorized);
        context.Response.ContentType.Should().Be("application/json");
        response.Error.Should().Be("Unauthorized");
        response.Message.Should().Be("Access denied. Please log in and try again.");
        response.CorrelationId.Should().Be("trace-123");
        structuredLogger.Verify(s => s.LogSystemEvent(
            "UnhandledException",
            It.Is<string>(message => message.Contains("GET") && message.Contains("/api/orders")),
            It.IsAny<object?>()), Times.Once);
    }

    [Fact]
    public async Task GlobalExceptionMiddleware_WithArgumentException_InDevelopment_ShouldIncludeDetails()
    {
        var structuredLogger = new Mock<IStructuredLoggingService>();
        var middleware = CreateGlobalExceptionMiddleware(
            _ => throw new ArgumentException("bad payload"),
            structuredLogger,
            Environments.Development);

        var context = CreateHttpContext(structuredLogger.Object, "/checkout/process", HttpMethods.Post);

        await middleware.InvokeAsync(context);

        var responseContent = await ReadResponseBodyAsync(context);
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        context.Response.ContentType.Should().Be("text/html; charset=utf-8");
        responseContent.Should().Contain("<!DOCTYPE html>");
        responseContent.Should().Contain("Bad Request");
        responseContent.Should().Contain("Invalid request data. Please check your input and try again.");
        responseContent.Should().Contain("trace-123");
        responseContent.Should().NotContain("bad payload");
    }

    [Fact]
    public async Task GlobalExceptionMiddleware_WithMissingResource_ShouldReturnNotFound()
    {
        var structuredLogger = new Mock<IStructuredLoggingService>();
        var middleware = CreateGlobalExceptionMiddleware(
            _ => throw new KeyNotFoundException("missing"),
            structuredLogger,
            Environments.Production);

        var context = CreateHttpContext(structuredLogger.Object, "/api/products/404", HttpMethods.Get);

        await middleware.InvokeAsync(context);

        var response = await ReadErrorResponseAsync(context);
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.NotFound);
        context.Response.ContentType.Should().Be("application/json");
        response.Error.Should().Be("Not Found");
        response.Message.Should().Be("The requested resource was not found.");
    }

    [Fact]
    public async Task GlobalExceptionMiddleware_WithDatabaseInvalidOperation_ShouldReturnServiceUnavailable()
    {
        var structuredLogger = new Mock<IStructuredLoggingService>();
        var middleware = CreateGlobalExceptionMiddleware(
            _ => throw new InvalidOperationException("database connection failed"),
            structuredLogger,
            Environments.Production);

        var context = CreateHttpContext(structuredLogger.Object, "/api/health", HttpMethods.Get);
        context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.22");

        await middleware.InvokeAsync(context);

        var response = await ReadErrorResponseAsync(context);
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.ServiceUnavailable);
        response.Error.Should().Be("Service Unavailable");
        response.Message.Should().Be("The service is temporarily unavailable. Please try again later.");
    }

    [Fact]
    public async Task GlobalExceptionMiddleware_WithTimeoutException_ShouldReturnRequestTimeout()
    {
        var structuredLogger = new Mock<IStructuredLoggingService>();
        var middleware = CreateGlobalExceptionMiddleware(
            _ => throw new TimeoutException("timed out"),
            structuredLogger,
            Environments.Production);

        var context = CreateHttpContext(structuredLogger.Object, "/api/search", HttpMethods.Get);

        await middleware.InvokeAsync(context);

        var response = await ReadErrorResponseAsync(context);
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.RequestTimeout);
        response.Error.Should().Be("Request Timeout");
        response.Message.Should().Be("The request took too long to process. Please try again.");
    }

    [Fact]
    public async Task GlobalExceptionMiddleware_WithSecurityException_ShouldReturnForbidden()
    {
        var structuredLogger = new Mock<IStructuredLoggingService>();
        var middleware = CreateGlobalExceptionMiddleware(
            _ => throw new SecurityException("forbidden"),
            structuredLogger,
            Environments.Production);

        var context = CreateHttpContext(structuredLogger.Object, "/admin", HttpMethods.Get);

        await middleware.InvokeAsync(context);

        var responseContent = await ReadResponseBodyAsync(context);
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.Forbidden);
        context.Response.ContentType.Should().Be("text/html; charset=utf-8");
        responseContent.Should().Contain("<!DOCTYPE html>");
        responseContent.Should().Contain("Forbidden");
        responseContent.Should().Contain("You don&#39;t have permission to access this resource.");
    }

    [Fact]
    public async Task GlobalExceptionMiddleware_WithUnexpectedException_InProduction_ShouldHideDetails()
    {
        var structuredLogger = new Mock<IStructuredLoggingService>();
        var middleware = CreateGlobalExceptionMiddleware(
            _ => throw new Exception("sensitive details"),
            structuredLogger,
            Environments.Production);

        var context = CreateHttpContext(structuredLogger.Object, "/products", HttpMethods.Get);
        context.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");

        await middleware.InvokeAsync(context);

        var responseContent = await ReadResponseBodyAsync(context);
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);
        context.Response.ContentType.Should().Be("text/html; charset=utf-8");
        responseContent.Should().Contain("<!DOCTYPE html>");
        responseContent.Should().Contain("Internal Server Error");
        responseContent.Should().Contain("An unexpected error occurred. Please try again later.");
        responseContent.Should().Contain("trace-123");
        responseContent.Should().NotContain("sensitive details");
    }

    [Fact]
    public async Task GlobalExceptionMiddleware_WithBrowserRequest_AndJsonAcceptHeader_ShouldReturnHtmlErrorPage()
    {
        var structuredLogger = new Mock<IStructuredLoggingService>();
        var middleware = CreateGlobalExceptionMiddleware(
            _ => throw new Exception("sensitive details"),
            structuredLogger,
            Environments.Production);

        var context = CreateHttpContext(structuredLogger.Object, "/products", HttpMethods.Get, "application/json");

        await middleware.InvokeAsync(context);

        var responseContent = await ReadResponseBodyAsync(context);
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);
        context.Response.ContentType.Should().Be("text/html; charset=utf-8");
        responseContent.Should().Contain("<!DOCTYPE html>");
        responseContent.Should().Contain("Internal Server Error");
        responseContent.Should().Contain("An unexpected error occurred. Please try again later.");
        responseContent.Should().Contain("trace-123");
        responseContent.Should().NotContain("sensitive details");
    }

    [Fact]
    public async Task GlobalExceptionMiddleware_WithApiPathAndBrowserAcceptHeader_ShouldStillReturnJson()
    {
        var structuredLogger = new Mock<IStructuredLoggingService>();
        var middleware = CreateGlobalExceptionMiddleware(
            _ => throw new KeyNotFoundException("missing"),
            structuredLogger,
            Environments.Production);

        var context = CreateHttpContext(structuredLogger.Object, "/api/products/404", HttpMethods.Get, "text/html");

        await middleware.InvokeAsync(context);

        var response = await ReadErrorResponseAsync(context);
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.NotFound);
        context.Response.ContentType.Should().Be("application/json");
        response.Error.Should().Be("Not Found");
    }

    [Fact]
    public async Task GlobalExceptionMiddleware_ShouldUseRemoteIpAddressInsteadOfRawForwardedHeaders()
    {
        var structuredLogger = new Mock<IStructuredLoggingService>();
        var middleware = CreateGlobalExceptionMiddleware(
            _ => throw new Exception("boom"),
            structuredLogger,
            Environments.Production);

        var context = CreateHttpContext(structuredLogger.Object, "/api/orders", HttpMethods.Get);
        context.Request.Headers["X-Forwarded-For"] = "198.51.100.50";
        context.Request.Headers["X-Real-IP"] = "198.51.100.51";
        context.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.20");

        await middleware.InvokeAsync(context);

        structuredLogger.Verify(s => s.LogSystemEvent(
            "UnhandledException",
            It.IsAny<string>(),
            It.Is<object?>(payload => payload != null && payload.ToString()!.Contains("127.0.0.20") && !payload.ToString()!.Contains("198.51.100.50") && !payload.ToString()!.Contains("198.51.100.51"))),
            Times.Once);
    }

    private static GlobalExceptionMiddleware CreateGlobalExceptionMiddleware(
        RequestDelegate next,
        Mock<IStructuredLoggingService> structuredLogger,
        string environmentName)
    {
        var logger = new Mock<ILogger<GlobalExceptionMiddleware>>();
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(environmentName);

        return new GlobalExceptionMiddleware(next, logger.Object, environment.Object);
    }

    private static DefaultHttpContext CreateHttpContext(
        IStructuredLoggingService structuredLoggingService,
        string path,
        string method,
        string acceptHeader = "text/html")
    {
        var services = new ServiceCollection()
            .AddSingleton(structuredLoggingService)
            .BuildServiceProvider();

        var context = new DefaultHttpContext
        {
            RequestServices = services,
            TraceIdentifier = "trace-123"
        };

        context.Request.Path = path;
        context.Request.Method = method;
        context.Request.Headers.Accept = acceptHeader;
        context.Request.Headers.UserAgent = "middleware-test-agent";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, "test-user")
        ], "TestAuth"));
        context.Response.Body = new MemoryStream();

        return context;
    }

    private static async Task<ErrorResponse> ReadErrorResponseAsync(DefaultHttpContext context)
    {
        var content = await ReadResponseBodyAsync(context);

        return JsonSerializer.Deserialize<ErrorResponse>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;
    }

    private static async Task<string> ReadResponseBodyAsync(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }
}
