using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;

namespace ELKH.Services;

/// <summary>
/// Service for optimizing images with compression, resizing, and lazy loading support
/// Implements modern image optimization techniques for web performance
/// </summary>
/// <remarks>
/// TABLE OF CONTENTS
/// ================================================================================
/// 1. Fields & Constructor                                         (lines 25-35)
/// 2. Public Optimization Methods                                  (lines 37-96)
///    - OptimizeImageAsync()                  // Core image optimization with WebP support
/// 3. Responsive Image Generation                                  (lines 98-145)
///    - CreateResponsiveImagesAsync()         // Generate multiple responsive sizes
/// 4. Placeholder Generation                                       (lines 147-180)
///    - GeneratePlaceholderAsync()            // Low-quality placeholder for lazy loading
/// 5. Utility Methods                                              (lines 182-215)
///    - GetOptimizedImagePath()               // Check for optimized version availability
/// ================================================================================
/// 
/// PERFORMANCE NOTES:
/// - Uses SixLabors.ImageSharp for cross-platform image processing
/// - WebP format provides 25-35% better compression than JPEG
/// - Responsive images reduce bandwidth on mobile devices
/// - Lazy loading placeholders improve perceived performance
/// </remarks>
public class ImageOptimizationService : IImageOptimizationService
{
    private readonly ILogger<ImageOptimizationService> _logger;
    private readonly IWebHostEnvironment _environment;

    // Standard responsive image sizes for different viewports
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

    /// <inheritdoc/>
    public async Task<Stream> OptimizeImageAsync(
        Stream inputStream, 
        string outputFormat = "webp", 
        int quality = 85, 
        int? maxWidth = null, 
        int? maxHeight = null)
    {
        try
        {
            var outputStream = new MemoryStream();
            
            using var image = await Image.LoadAsync(inputStream);
            
            // Resize if dimensions are specified
            if (maxWidth.HasValue || maxHeight.HasValue)
            {
                var resizeOptions = new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(maxWidth ?? image.Width, maxHeight ?? image.Height)
                };
                
                image.Mutate(x => x.Resize(resizeOptions));
            }

            // Apply format-specific encoding with quality settings
            switch (outputFormat.ToLowerInvariant())
            {
                case "webp":
                    var webpEncoder = new WebpEncoder { Quality = quality };
                    await image.SaveAsWebpAsync(outputStream, webpEncoder);
                    break;
                    
                case "jpg":
                case "jpeg":
                    var jpegEncoder = new JpegEncoder { Quality = quality };
                    await image.SaveAsJpegAsync(outputStream, jpegEncoder);
                    break;
                    
                case "png":
                    var pngEncoder = new PngEncoder();
                    await image.SaveAsPngAsync(outputStream, pngEncoder);
                    break;
                    
                default:
                    throw new ArgumentException($"Unsupported output format: {outputFormat}");
            }

            outputStream.Position = 0;
            
            _logger.LogDebug("Optimized image from {InputSize}KB to {OutputSize}KB ({Format}, Q{Quality})",
                inputStream.Length / 1024, outputStream.Length / 1024, outputFormat, quality);
                
            return outputStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to optimize image with format {Format} and quality {Quality}", outputFormat, quality);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<Dictionary<string, string>> CreateResponsiveImagesAsync(
        Stream inputStream, 
        string baseFileName, 
        string outputDirectory)
    {
        var results = new Dictionary<string, string>();

        try
        {
            // Ensure output directory exists
            var fullOutputPath = Path.Combine(_environment.WebRootPath, outputDirectory.TrimStart('/'));
            Directory.CreateDirectory(fullOutputPath);

            using var originalImage = await Image.LoadAsync(inputStream);
            
            foreach (var size in ResponsiveSizes)
            {
                var sizeName = size.Key;
                var maxDimension = size.Value;
                
                // Skip if original is smaller than target size
                if (originalImage.Width <= maxDimension && originalImage.Height <= maxDimension && sizeName != "thumbnail")
                    continue;

                var fileName = $"{baseFileName}-{sizeName}.webp";
                var filePath = Path.Combine(fullOutputPath, fileName);
                var webPath = $"/{outputDirectory.TrimStart('/')}/{fileName}".Replace('\\', '/');

                // Create optimized version for this size
                inputStream.Position = 0;
                using var optimizedStream = await OptimizeImageAsync(inputStream, "webp", 85, maxDimension, maxDimension);
                
                // Save to disk
                using var fileStream = new FileStream(filePath, FileMode.Create);
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

    /// <inheritdoc/>
    public async Task<string> GeneratePlaceholderAsync(Stream inputStream, int width = 20, int height = 20)
    {
        try
        {
            using var image = await Image.LoadAsync(inputStream);
            
            // Resize to tiny placeholder size
            image.Mutate(x => x.Resize(width, height));
            
            // Encode as base64 JPEG with very low quality for tiny size
            using var outputStream = new MemoryStream();
            var jpegEncoder = new JpegEncoder { Quality = 10 };
            await image.SaveAsJpegAsync(outputStream, jpegEncoder);
            
            var base64 = Convert.ToBase64String(outputStream.ToArray());
            var dataUrl = $"data:image/jpeg;base64,{base64}";
            
            _logger.LogDebug("Generated placeholder of {Size} bytes", outputStream.Length);
            
            return dataUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate placeholder image");
            
            // Return a default tiny transparent pixel as fallback
            return "data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7";
        }
    }

    /// <inheritdoc/>
    public string GetOptimizedImagePath(string originalPath, string format = "webp")
    {
        try
        {
            if (string.IsNullOrEmpty(originalPath))
                return originalPath;

            // Parse the original path
            var directory = Path.GetDirectoryName(originalPath) ?? "";
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(originalPath);
            
            // Construct optimized path
            var optimizedFileName = $"{fileNameWithoutExt}.{format}";
            var optimizedPath = Path.Combine(directory, "optimized", optimizedFileName).Replace('\\', '/');
            
            // Check if optimized version exists
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
}