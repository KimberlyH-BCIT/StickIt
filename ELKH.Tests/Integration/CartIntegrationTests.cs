using FluentAssertions;
using System.Net.Http;
using System.Text;
using Xunit;

namespace ELKH.Tests.Integration;

/// <summary>
/// Integration tests for current Cart routes and anonymous-user behavior.
/// </summary>
public class CartIntegrationTests : IClassFixture<ELKHWebApplicationFactory>
{
    private readonly ELKHWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CartIntegrationTests(ELKHWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Cart_Index_WithoutAuth_ShouldReturnGuestCartView()
    {
        var response = await _client.GetAsync("/Cart");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/Cart/AddToCart", "itemId=1&quantity=1")]
    [InlineData("/Cart/Update", "cartId=1&quantity=2")]
    [InlineData("/Cart/Remove", "cartId=1")]
    [InlineData("/Cart/Clear", "")]
    public async Task CartStateChangingPosts_WithoutAntiforgeryToken_ShouldReturnBadRequest(string url, string formData)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(formData, Encoding.UTF8, "application/x-www-form-urlencoded")
        };

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PlaceOrder_WithoutAuth_ShouldRedirectToGuestCheckout()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Cart/PlaceOrder")
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/x-www-form-urlencoded")
        };

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task BuyNow_WithoutAuth_ShouldReturnBadRequestWithoutAntiforgeryToken()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Cart/BuyNow")
        {
            Content = new StringContent("itemId=1&quantity=1", Encoding.UTF8, "application/x-www-form-urlencoded")
        };

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}