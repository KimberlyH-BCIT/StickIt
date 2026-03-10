using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace ELKH.Models;

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
