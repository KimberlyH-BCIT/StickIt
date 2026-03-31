using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;

namespace ELKH.Services;

/// <summary>
/// Interface for image optimization services including compression and lazy loading support
/// </summary>
public interface IImageOptimizationService
{
    /// <summary>
    /// Optimizes an image by compressing it and optionally resizing
    /// </summary>
    /// <param name="inputStream">Original image stream</param>
    /// <param name="outputFormat">Desired output format (webp, jpg, png)</param>
    /// <param name="quality">Compression quality (1-100)</param>
    /// <param name="maxWidth">Maximum width in pixels (optional)</param>
    /// <param name="maxHeight">Maximum height in pixels (optional)</param>
    /// <returns>Optimized image stream</returns>
    Task<Stream> OptimizeImageAsync(
        Stream inputStream, 
        string outputFormat = "webp", 
        int quality = 85, 
        int? maxWidth = null, 
        int? maxHeight = null);

    /// <summary>
    /// Creates multiple responsive image sizes for lazy loading
    /// </summary>
    /// <param name="inputStream">Original image stream</param>
    /// <param name="baseFileName">Base filename without extension</param>
    /// <param name="outputDirectory">Directory to save optimized images</param>
    /// <returns>Dictionary of size names and their file paths</returns>
    Task<Dictionary<string, string>> CreateResponsiveImagesAsync(
        Stream inputStream, 
        string baseFileName, 
        string outputDirectory);

    /// <summary>
    /// Generates base64-encoded low quality placeholder image for lazy loading
    /// </summary>
    /// <param name="inputStream">Original image stream</param>
    /// <param name="width">Placeholder width (default 20px)</param>
    /// <param name="height">Placeholder height (default 20px)</param>
    /// <returns>Base64 data URL for placeholder</returns>
    Task<string> GeneratePlaceholderAsync(
        Stream inputStream, 
        int width = 20, 
        int height = 20);

    /// <summary>
    /// Gets the optimized file path for an image if it exists
    /// </summary>
    /// <param name="originalPath">Original image path</param>
    /// <param name="format">Desired format</param>
    /// <returns>Optimized image path or original if optimization not available</returns>
    string GetOptimizedImagePath(string originalPath, string format = "webp");
}