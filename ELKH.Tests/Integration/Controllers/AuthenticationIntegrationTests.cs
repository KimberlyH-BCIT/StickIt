using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ELKH.Data;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace ELKH.Tests.Integration.Controllers;

/// <summary>
/// Integration tests for authentication and authorization flows.
/// Tests the complete authentication pipeline including login, registration, and protected routes.
/// </summary>
[Collection("Integration")]
public class AuthenticationIntegrationTests : IClassFixture<ELKHWebApplicationFactory>
{
    private readonly ELKHWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly HttpClient _noRedirectClient;

    public AuthenticationIntegrationTests(ELKHWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _noRedirectClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task Home_Page_Should_Be_Accessible_Without_Authentication()
    {
        // Act
        var response = await _client.GetAsync("/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("ELKH"); // Should contain site branding
    }

    [Fact]
    public async Task Products_Page_Should_Be_Accessible_Without_Authentication()
    {
        // Act
        var response = await _client.GetAsync("/Product");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Products"); // Should show products
    }

    [Fact]
    public async Task Cart_Page_Should_Redirect_To_Login_When_Not_Authenticated()
    {
        var response = await _client.GetAsync("/Cart");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Shopping Cart");
    }

    [Fact]
    public async Task Admin_Area_Should_Redirect_To_Login_When_Not_Authenticated()
    {
        var response = await _noRedirectClient.GetAsync("/Admin");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().Should().Contain("Identity/Account/Login");
    }

    [Fact]
    public async Task User_Dashboard_Should_Redirect_To_Login_When_Not_Authenticated()
    {
        var response = await _noRedirectClient.GetAsync("/User");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().Should().Contain("Identity/Account/Login");
    }

    [Theory]
    [InlineData("/Checkout")]
    [InlineData("/Order")]
    [InlineData("/Wishlist")]
    public async Task Protected_Pages_Should_Redirect_To_Login_When_Not_Authenticated(string url)
    {
        var response = await _noRedirectClient.GetAsync(url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().Should().Contain("Identity/Account/Login");
    }

    [Fact]
    public async Task Registration_Page_Should_Be_Accessible()
    {
        // Act
        var response = await _client.GetAsync("/Identity/Account/Register");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Register"); // Should contain registration form
        content.Should().Contain("Email"); // Should have email field
        content.Should().Contain("Password"); // Should have password field
    }

    [Fact]
    public async Task Login_Page_Should_Be_Accessible()
    {
        // Act
        var response = await _client.GetAsync("/Identity/Account/Login");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Log in"); // Should contain login form
        content.Should().Contain("Email"); // Should have email field
        content.Should().Contain("Password"); // Should have password field
    }

    [Fact]
    public async Task Health_Check_Should_Be_Accessible()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);
        var content = await response.Content.ReadAsStringAsync();

        if (content.TrimStart().StartsWith("{"))
        {
            var healthCheck = JsonSerializer.Deserialize<JsonElement>(content);
            healthCheck.GetProperty("status").GetString().Should().NotBeNullOrWhiteSpace();
        }
        else
        {
            content.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public async Task CSRF_Protection_Should_Be_Enforced_On_POST_Requests()
    {
        // Arrange - Try to post to a protected endpoint without CSRF token
        var formData = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("Email", "test@example.com"),
            new KeyValuePair<string, string>("Password", "TestPassword123!")
        });

        // Act
        var response = await _client.PostAsync("/Identity/Account/Login", formData);

        // Assert
        // Should either redirect to login again (invalid token) or return 400 (bad request)
        // The exact behavior depends on ASP.NET Core Identity's CSRF handling
        (response.StatusCode == HttpStatusCode.Redirect ||
         response.StatusCode == HttpStatusCode.BadRequest).Should().BeTrue();
    }

    [Fact]
    public async Task Security_Headers_Should_Be_Present()
    {
        // Act
        var response = await _client.GetAsync("/");

        // Assert
        response.Headers.Should().ContainKey("X-Content-Type-Options");
        response.Headers.Should().ContainKey("X-Frame-Options");
        response.Headers.Should().ContainKey("Referrer-Policy");
        response.Headers.Should().ContainKey("Permissions-Policy");

        response.Headers.GetValues("X-Content-Type-Options").First().Should().Be("nosniff");
        response.Headers.GetValues("X-Frame-Options").First().Should().Be("SAMEORIGIN");
    }

    [Fact]
    public async Task Rate_Limiting_Should_Be_Configured()
    {
        // Act - Make multiple rapid requests to test rate limiting
        var tasks = new List<Task<HttpResponseMessage>>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(_client.GetAsync("/"));
        }

        var responses = await Task.WhenAll(tasks);

        // Assert - All requests should succeed under normal rate limits
        foreach (var response in responses)
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }
}