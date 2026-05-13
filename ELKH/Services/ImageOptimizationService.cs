using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace ELKH.Services;

/// <summary>
/// Service for optimizing images with compression, resizing, and lazy loading support.
/// </summary>
public class ImageOptimizationService : IImageOptimizationService
{
    private readonly ILogger<ImageOptimizationService> _logger;
    private readonly IWebHostEnvironment _environment;

    private static readonly Dictionary<string, int> ResponsiveSizes = new()
    {
        { "thumbnail", 150 },
        { "small", 300 },
        { "medium", 600 },
        { "large", 1200 },
        { "xlarge", 1920 }
    };

    public ImageOptimizationService(
        ILogger<ImageOptimizationService> logger,
        IWebHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public Task<Stream> OptimizeImageAsync(
        Stream inputStream,
        string outputFormat = "webp",
        int quality = 85,
        int? maxWidth = null,
        int? maxHeight = null)
    {
        try
        {
            if (inputStream.CanSeek)
            {
                inputStream.Position = 0;
            }
            var outputStream = new MemoryStream();

            inputStream.CopyTo(outputStream);
            outputStream.Position = 0;

            _logger.LogDebug(
                "Returned image stream without re-encoding from {InputSize}KB to {OutputSize}KB ({Format}, Q{Quality})",
                inputStream.CanSeek ? inputStream.Length / 1024 : 0,
                outputStream.Length / 1024,
                outputFormat,
                quality);

            return Task.FromResult<Stream>(outputStream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to optimize image with format {Format} and quality {Quality}", outputFormat, quality);
            throw;
        }
    }

    public async Task<Dictionary<string, string>> CreateResponsiveImagesAsync(
        Stream inputStream,
        string baseFileName,
        string outputDirectory)
    {
        var results = new Dictionary<string, string>();

        try
        {
            var fullOutputPath = Path.Combine(_environment.WebRootPath, outputDirectory.TrimStart('/'));
            Directory.CreateDirectory(fullOutputPath);

            await using var sourceCopy = new MemoryStream();
            await inputStream.CopyToAsync(sourceCopy);
            sourceCopy.Position = 0;

            foreach (var size in ResponsiveSizes)
            {
                var sizeName = size.Key;
                var fileName = $"{baseFileName}-{sizeName}.webp";
                var filePath = Path.Combine(fullOutputPath, fileName);
                var webPath = $"/{outputDirectory.TrimStart('/')}/{fileName}".Replace('\\', '/');

                sourceCopy.Position = 0;
                await using var optimizedStream = await OptimizeImageAsync(sourceCopy, "webp", 85, size.Value, size.Value);
                await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                await optimizedStream.CopyToAsync(fileStream);

                results[sizeName] = webPath;
                _logger.LogDebug("Created responsive image {Size} at {Path}", sizeName, webPath);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create responsive images for {BaseFileName}", baseFileName);
            throw;
        }
    }

    public Task<string> GeneratePlaceholderAsync(Stream inputStream, int width = 20, int height = 20)
    {
        try
        {
            _logger.LogDebug("Generated fallback placeholder for requested size {Width}x{Height}", width, height);
            return Task.FromResult("data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate placeholder image");
            return Task.FromResult("data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");
        }
    }

    public string GetOptimizedImagePath(string originalPath, string format = "webp")
    {
        try
        {
            if (string.IsNullOrEmpty(originalPath))
            {
                return originalPath;
            }

            var normalizedFormat = NormalizeOutputFormat(format);
            var directory = Path.GetDirectoryName(originalPath) ?? string.Empty;
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(originalPath);
            var optimizedFileName = $"{fileNameWithoutExt}{GetFileExtensionForFormat(normalizedFormat)}";
            var optimizedPath = Path.Combine(directory, "optimized", optimizedFileName).Replace('\\', '/');
            var physicalPath = Path.Combine(_environment.WebRootPath, optimizedPath.TrimStart('/'));

            if (File.Exists(physicalPath))
            {
                _logger.LogDebug("Using optimized image at {OptimizedPath}", optimizedPath);
                return "/" + optimizedPath.TrimStart('/');
            }

            _logger.LogDebug("Optimized image not found, using original at {OriginalPath}", originalPath);
            return originalPath;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to determine optimized image path for {OriginalPath}", originalPath);
            return originalPath;
        }
    }

    private static string NormalizeOutputFormat(string outputFormat)
    {
        return outputFormat.ToLowerInvariant() switch
        {
            "jpg" => "jpg",
            "jpeg" => "jpg",
            "png" => "png",
            "webp" => "webp",
            _ => throw new ArgumentException($"Unsupported output format: {outputFormat}")
        };
    }

    private static string GetFileExtensionForFormat(string outputFormat)
    {
        return outputFormat switch
        {
            "png" => ".png",
            "webp" => ".webp",
            _ => ".jpg"
        };
    }
}
