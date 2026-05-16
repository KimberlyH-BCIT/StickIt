using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ELKH.Models;

/// <summary>
/// Design-time database context factory for ImageStoreContext providing Entity Framework
/// migrations support and database context creation during development and deployment.
/// </summary>
public class ImageStoreContextFactory : IDesignTimeDbContextFactory<ImageStoreContext>
{
    public ImageStoreContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();
        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = config.GetConnectionString("ImageStoreConnection") ?? "Data Source=ImageStore.db";

        var optionsBuilder = new DbContextOptionsBuilder<ImageStoreContext>();
        optionsBuilder.UseSqlite(connectionString);

        return new ImageStoreContext(optionsBuilder.Options);
    }
}
