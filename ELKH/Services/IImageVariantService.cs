namespace ELKH.Services;

/// <summary>
/// Interface for image variant/path services used by the UI for responsive image paths,
/// pass-through stream copies, and lightweight placeholders.
/// </summary>
public interface IImageVariantService
{
    /// <summary>
    /// Copies an image stream into an output stream without guaranteed re-encoding or resizing.
    /// Parameters are retained so callers can request preferred output metadata even though the
    /// current implementation is pass-through.
    /// </summary>
    /// <param name="inputStream">Original image stream.</param>
    /// <param name="outputFormat">Requested output format hint.</param>
    /// <param name="quality">Requested quality hint.</param>
    /// <param name="maxWidth">Requested maximum width hint.</param>
    /// <param name="maxHeight">Requested maximum height hint.</param>
    /// <returns>Copied image stream.</returns>
    Task<Stream> OptimizeImageAsync(
        Stream inputStream,
        string outputFormat = "webp",
        int quality = 85,
        int? maxWidth = null,
        int? maxHeight = null);

    /// <summary>
    /// Creates multiple responsive image files using the current pass-through copy behavior.
    /// </summary>
    /// <param name="inputStream">Original image stream.</param>
    /// <param name="baseFileName">Base filename without extension.</param>
    /// <param name="outputDirectory">Directory to save responsive variants.</param>
    /// <returns>Dictionary of size names and their file paths.</returns>
    Task<Dictionary<string, string>> CreateResponsiveImagesAsync(
        Stream inputStream,
        string baseFileName,
        string outputDirectory);

    /// <summary>
    /// Generates a lightweight placeholder data URL for lazy loading fallbacks.
    /// </summary>
    /// <param name="inputStream">Original image stream.</param>
    /// <param name="width">Placeholder width.</param>
    /// <param name="height">Placeholder height.</param>
    /// <returns>Base64 data URL placeholder.</returns>
    Task<string> GeneratePlaceholderAsync(
        Stream inputStream,
        int width = 20,
        int height = 20);

    /// <summary>
    /// Gets the preferred optimized or variant image path if one exists, otherwise returns the original path.
    /// </summary>
    /// <param name="originalPath">Original image path.</param>
    /// <param name="format">Desired output format.</param>
    /// <returns>Variant image path or original if no variant is available.</returns>
    string GetOptimizedImagePath(string originalPath, string format = "webp");
}
