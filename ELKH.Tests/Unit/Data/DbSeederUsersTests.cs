using System.IO;
using ELKH.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace ELKH.Tests.Unit.Data;

/// <summary>
/// Unit tests for the user and role seeding guardrails.
/// </summary>
public class DbSeederUsersTests : IDisposable
{
    private readonly string _databaseName = $"DbSeederUsersTests_{Guid.NewGuid():N}";

    [Fact]
    public async Task SeedUsersAndRolesAsync_WhenDefaultsAreDisabledAndCredentialsAreMissing_ShouldThrow()
    {
        await using var db = CreateContext();
        var userManager = CreateMockUserManager();
        var roleManager = CreateMockRoleManager();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        Func<Task> act = () => DbSeeder.SeedUsersAndRolesAsync(
            db,
            userManager.Object,
            roleManager.Object,
            configuration,
            Path.GetTempPath(),
            allowDefaultElevatedCredentials: false);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*require explicit credentials*");
    }

    [Fact]
    public async Task SeedUsersAndRolesAsync_WhenDefaultsAreDisabledAndBuiltInDemoCredentialsAreConfigured_ShouldThrow()
    {
        await using var db = CreateContext();
        var userManager = CreateMockUserManager();
        var roleManager = CreateMockRoleManager();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:AdminEmail"] = "admin@stickit.dev",
                ["Seed:AdminPass"] = "Admin@2025!",
                ["Seed:ManagerEmail"] = "manager@stickit.dev",
                ["Seed:ManagerPass"] = "Manager@2025!",
                ["Seed:StaffEmail"] = "staff@stickit.dev",
                ["Seed:StaffPass"] = "Staff@2025!"
            })
            .Build();

        Func<Task> act = () => DbSeeder.SeedUsersAndRolesAsync(
            db,
            userManager.Object,
            roleManager.Object,
            configuration,
            Path.GetTempPath(),
            allowDefaultElevatedCredentials: false);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cannot use built-in demo credentials*");
    }

    [Fact]
    public async Task SeedUsersAndRolesAsync_WhenDefaultsAreEnabled_ShouldAllowBuiltInDemoCredentials()
    {
        await using var db = CreateContext();
        var userManager = CreateMockUserManager();
        var roleManager = CreateMockRoleManager();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        roleManager.Setup(r => r.RoleExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        roleManager.Setup(r => r.CreateAsync(It.IsAny<IdentityRole>())).ReturnsAsync(IdentityResult.Success);

        userManager.Setup(u => u.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((IdentityUser?)null);
        userManager.Setup(u => u.CreateAsync(It.IsAny<IdentityUser>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);
        userManager.Setup(u => u.IsInRoleAsync(It.IsAny<IdentityUser>(), It.IsAny<string>())).ReturnsAsync(false);
        userManager.Setup(u => u.AddToRoleAsync(It.IsAny<IdentityUser>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);

        Func<Task> act = () => DbSeeder.SeedUsersAndRolesAsync(
            db,
            userManager.Object,
            roleManager.Object,
            configuration,
            Path.GetTempPath(),
            allowDefaultElevatedCredentials: true);

        await act.Should().NotThrowAsync();
        userManager.Verify(u => u.CreateAsync(It.IsAny<IdentityUser>(), "Admin@2025!"), Times.Once);
        userManager.Verify(u => u.CreateAsync(It.IsAny<IdentityUser>(), "Manager@2025!"), Times.Once);
        userManager.Verify(u => u.CreateAsync(It.IsAny<IdentityUser>(), "Staff@2025!"), Times.Once);
    }

    private ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(_databaseName)
            .Options;

        return new ApplicationDbContext(options);
    }

    private static Mock<UserManager<IdentityUser>> CreateMockUserManager()
    {
        var store = new Mock<IUserStore<IdentityUser>>();
        return new Mock<UserManager<IdentityUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    private static Mock<RoleManager<IdentityRole>> CreateMockRoleManager()
    {
        var store = new Mock<IRoleStore<IdentityRole>>();
        return new Mock<RoleManager<IdentityRole>>(
            store.Object, null!, null!, null!, null!);
    }

    public void Dispose()
    {
        using var db = CreateContext();
        db.Database.EnsureDeleted();
    }
}
