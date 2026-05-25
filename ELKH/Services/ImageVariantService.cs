using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace ELKH.Services;

// TABLE OF CONTENTS
// - Variant generation
// - Image sizing
// - Derivative handling

/// <summary>
/// Service for pass-through image copies, responsive variant file generation, and placeholder support.
/// It does not currently perform guaranteed re-encoding or resizing.
/// </summary>
public class ImageVariantService : IImageVariantService
{
    private readonly ILogger<ImageVariantService> _logger;
    private readonly IWebHostEnvironment _environment;

    private static readonly Dictionary<string, int> ResponsiveSizes = new()
    {
        { "thumbnail", 150 },
        { "small", 300 },
        { "medium", 600 },
        { "large", 1200 },
        { "xlarge", 1920 }
    };

    private const string PlaceholderDataUri = "data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7";

    public ImageVariantService(
        ILogger<ImageVariantService> logger,
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
                "Returned copied image stream without guaranteed re-encoding from {InputSize}KB to {OutputSize}KB ({Format}, Q{Quality})",
                inputStream.CanSeek ? inputStream.Length / 1024 : 0,
                outputStream.Length / 1024,
                outputFormat,
                quality);

            return Task.FromResult<Stream>(outputStream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to prepare image variant stream with format {Format} and quality {Quality}", outputFormat, quality);
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
                await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                await sourceCopy.CopyToAsync(fileStream);

                results[sizeName] = webPath;
                _logger.LogDebug("Created responsive image variant {Size} at {Path}", sizeName, webPath);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create responsive image variants for {BaseFileName}", baseFileName);
            throw;
        }
    }

    public Task<string> GeneratePlaceholderAsync(Stream inputStream, int width = 20, int height = 20)
    {
        try
        {
            _logger.LogDebug("Generated fallback placeholder for requested size {Width}x{Height}", width, height);
            return Task.FromResult(PlaceholderDataUri);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate placeholder image");
            return Task.FromResult(PlaceholderDataUri);
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
                _logger.LogDebug("Using image variant at {OptimizedPath}", optimizedPath);
                return "/" + optimizedPath.TrimStart('/');
            }

            _logger.LogDebug("Image variant not found, using original at {OriginalPath}", originalPath);
            return originalPath;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to determine image variant path for {OriginalPath}", originalPath);
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
