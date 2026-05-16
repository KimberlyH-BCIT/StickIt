using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using ELKH.Tests.Utilities;

namespace ELKH.Tests.Basic;

/// <summary>
/// Basic infrastructure tests to validate test setup and core functionality.
/// </summary>
public class InfrastructureTests : BaseTest
{
    [Fact]
    public void Database_ShouldBeAvailable()
    {
        // Arrange & Act
        var canConnect = _context.Database.CanConnect();

        // Assert
        canConnect.Should().BeTrue();
    }

    [Fact]
    public void TestDataFactory_ShouldCreateValidUser()
    {
        // Arrange & Act
        var user = TestDataFactory.CreateUser();

        // Assert
        user.Should().NotBeNull();
        user.Email.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void TestDataFactory_ShouldCreateValidProduct()
    {
        // Arrange & Act
        var product = TestDataFactory.CreateProduct();

        // Assert
        product.Should().NotBeNull();
        product.Name.Should().NotBeNullOrEmpty();
        product.Price.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Database_ShouldAllowCrudOperations()
    {
        // Arrange
        var user = TestDataFactory.CreateUser();

        // Act - Create
        _context.RegisteredUsers.Add(user);
        await _context.SaveChangesAsync();

        // Act - Read
        var retrievedUser = await _context.RegisteredUsers.FindAsync(user.PkRegisteredUserId);

        // Assert
        retrievedUser.Should().NotBeNull();
        retrievedUser!.Email.Should().Be(user.Email);
    }
}