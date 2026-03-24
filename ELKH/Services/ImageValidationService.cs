using SixLabors.ImageSharp;

namespace ELKH.Services;

/// <summary>
/// Secure image file validation service with multi-layer security checks.
/// Provides defense-in-depth against malicious file uploads through:
/// - Magic byte validation (file signature verification)
/// - Content-Type validation
/// - File extension whitelisting
/// - Image dimension validation
/// - File size limits
/// </summary>
/// <remarks>
/// SECURITY RATIONALE:
/// File upload vulnerabilities are among the OWASP Top 10 risks. This service implements
/// multiple layers of validation because:
/// 
/// 1. Extension validation alone is insufficient (easily spoofed)
/// 2. Content-Type headers can be manipulated by clients
/// 3. Magic byte validation prevents disguised executables
/// 4. Dimension limits prevent memory exhaustion attacks
/// 5. Size limits prevent storage/bandwidth DoS
/// 
/// All validations must pass for a file to be accepted.
/// </remarks>
public class ImageValidationService
{
    private readonly ILogger<ImageValidationService> _logger;

    // Magic byte signatures for common image formats
    // These byte sequences appear at the start of valid image files
    private static readonly Dictionary<string, byte[][]> MagicBytes = new()
    {
        { ".jpg", new[] { new byte[] { 0xFF, 0xD8, 0xFF } } },
        { ".jpeg", new[] { new byte[] { 0xFF, 0xD8, 0xFF } } },
        { ".png", new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } } },
        { ".gif", new[] { new byte[] { 0x47, 0x49, 0x46, 0x38, 0x37, 0x61 }, // GIF87a
                          new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 } } }, // GIF89a
        { ".webp", new[] { new byte[] { 0x52, 0x49, 0x46, 0x46 } } }, // "RIFF" (WebP header, bytes 8-11 are "WEBP")
        { ".bmp", new[] { new byte[] { 0x42, 0x4D } } } // "BM"
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp"
    };

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/gif", "image/webp", "image/bmp"
    };

    // Security limits
    private const int MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB
    private const int MaxImageWidth = 4096;
    private const int MaxImageHeight = 4096;
    private const int MaxFileNameLength = 100;

    public ImageValidationService(ILogger<ImageValidationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validates an uploaded image file against all security checks.
    /// </summary>
    /// <param name="file">The uploaded file from the HTTP request</param>
    /// <returns>A validation result containing success status and error messages</returns>
    public async Task<ImageValidationResult> ValidateImageAsync(IFormFile file)
    {
        var result = new ImageValidationResult();

        // ═══════════════════════════════════════════════════════════════════
        // Check 1: File existence and size
        // ═══════════════════════════════════════════════════════════════════
        if (file == null || file.Length == 0)
        {
            result.Errors.Add("No file was uploaded.");
            return result;
        }

        if (file.Length > MaxFileSizeBytes)
        {
            result.Errors.Add($"File size ({file.Length / 1024 / 1024:F2} MB) exceeds maximum allowed size ({MaxFileSizeBytes / 1024 / 1024} MB).");
            return result;
        }

        // ═══════════════════════════════════════════════════════════════════
        // Check 2: File extension validation
        // ═══════════════════════════════════════════════════════════════════
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
        {
            result.Errors.Add($"File extension '{extension}' is not allowed. Supported formats: jpg, jpeg, png, gif, webp, bmp.");
            return result;
        }

        // ═══════════════════════════════════════════════════════════════════
        // Check 3: MIME type validation
        // ═══════════════════════════════════════════════════════════════════
        if (!AllowedMimeTypes.Contains(file.ContentType))
        {
            result.Errors.Add($"Content type '{file.ContentType}' is not allowed for images.");
            return result;
        }

        // ═══════════════════════════════════════════════════════════════════
        // Check 4: Magic byte validation (file signature)
        // This prevents malicious executables disguised with image extensions
        // ═══════════════════════════════════════════════════════════════════
        try
        {
            using var stream = file.OpenReadStream();
            var header = new byte[8]; // Read first 8 bytes for signature check
            var bytesRead = await stream.ReadAsync(header, 0, header.Length);

            if (bytesRead < header.Length)
            {
                result.Errors.Add("File is too small to be a valid image.");
                return result;
            }

            if (!ValidateMagicBytes(header, extension))
            {
                result.Errors.Add($"File signature does not match expected format for {extension} files. The file may be corrupted or disguised as an image.");
                _logger.LogWarning("Magic byte validation failed for file {FileName} with extension {Extension}", 
                    file.FileName, extension);
                return result;
            }

            // Reset stream position for subsequent processing
            stream.Position = 0;

            // ═══════════════════════════════════════════════════════════════
            // Check 5: Image dimension validation
            // Prevents memory exhaustion attacks from extremely large images
            // ═══════════════════════════════════════════════════════════════
            try
            {
                using var image = await Image.LoadAsync(stream);
                
                if (image.Width > MaxImageWidth || image.Height > MaxImageHeight)
                {
                    result.Errors.Add($"Image dimensions ({image.Width}x{image.Height}) exceed maximum allowed size ({MaxImageWidth}x{MaxImageHeight}).");
                    return result;
                }

                result.ImageWidth = image.Width;
                result.ImageHeight = image.Height;
            }
            catch (Exception ex)
            {
                result.Errors.Add("File is not a valid image or is corrupted.");
                _logger.LogWarning(ex, "Failed to load image from file {FileName}", file.FileName);
                return result;
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add("An error occurred while validating the file.");
            _logger.LogError(ex, "Unexpected error during image validation for file {FileName}", file.FileName);
            return result;
        }

        // ═══════════════════════════════════════════════════════════════════
        // Check 6: Filename validation and sanitization
        // ═══════════════════════════════════════════════════════════════════
        var fileName = Path.GetFileNameWithoutExtension(file.FileName);
        if (fileName.Length > MaxFileNameLength)
        {
            result.Errors.Add($"Filename is too long (max {MaxFileNameLength} characters).");
            return result;
        }

        result.SanitizedFileName = SanitizeFileName(fileName) + extension;

        // All checks passed
        result.IsValid = true;
        return result;
    }

    /// <summary>
    /// Validates file signature (magic bytes) against expected values for the file extension.
    /// </summary>
    private bool ValidateMagicBytes(byte[] fileHeader, string extension)
    {
        if (!MagicBytes.TryGetValue(extension, out var signatures))
            return false;

        foreach (var signature in signatures)
        {
            if (fileHeader.Length < signature.Length)
                continue;

            bool matches = true;
            for (int i = 0; i < signature.Length; i++)
            {
                if (fileHeader[i] != signature[i])
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Sanitizes a filename by removing special characters and limiting length.
    /// Prevents directory traversal and filesystem attacks.
    /// </summary>
    private string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return $"image_{Guid.NewGuid():N}";

        // Remove any path separators to prevent directory traversal
        fileName = fileName.Replace("/", "").Replace("\\", "").Replace("..", "");

        // Keep only alphanumeric, underscores, hyphens, and spaces
        var sanitized = new string(fileName
            .Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == ' ')
            .ToArray());

        // Replace spaces with underscores
        sanitized = sanitized.Replace(' ', '_');

        // If sanitization removed everything, generate a GUID-based name
        if (string.IsNullOrWhiteSpace(sanitized))
            return $"image_{Guid.NewGuid():N}";

        // Truncate if too long
        if (sanitized.Length > MaxFileNameLength)
            sanitized = sanitized.Substring(0, MaxFileNameLength);

        return sanitized;
    }
}

/// <summary>
/// Result of image validation operation.
/// </summary>
public class ImageValidationResult
{
    /// <summary>True if all validation checks passed</summary>
    public bool IsValid { get; set; }

    /// <summary>List of validation errors (empty if IsValid is true)</summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>Sanitized filename safe for storage</summary>
    public string SanitizedFileName { get; set; } = string.Empty;

    /// <summary>Validated image width in pixels</summary>
    public int ImageWidth { get; set; }

    /// <summary>Validated image height in pixels</summary>
    public int ImageHeight { get; set; }

    /// <summary>Gets first error message or empty string</summary>
    public string FirstError => Errors.FirstOrDefault() ?? string.Empty;
}
