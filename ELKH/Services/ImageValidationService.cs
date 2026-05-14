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
    /// Validates an uploaded image file against comprehensive security and format checks.
    /// </summary>
    /// <param name="file">The uploaded file from the HTTP request (IFormFile). 
    /// Can be null (validation will fail gracefully).
    /// Must represent an image file with valid content, extension, and magic bytes.</param>
    /// <returns>
    /// Returns an ImageValidationResult containing:
    /// <list type="bullet">
    /// <item>IsValid - boolean indicating if all validations passed</item>
    /// <item>Errors - List of human-readable error messages if validation fails</item>
    /// <item>SanitizedFileName - Safe filename for storage if validation succeeds</item>
    /// <item>Width/Height - Image dimensions if successfully processed</item>
    /// </list>
    /// The result is always non-null, even for null input files.
    /// </returns>
    /// <remarks>
    /// <para><strong>Comprehensive Validation Process:</strong></para>
    /// <list type="number">
    /// <item>File existence and size validation (max 5MB)</item>
    /// <item>File extension validation (jpg, jpeg, png, gif, webp, bmp only)</item>
    /// <item>MIME type validation (prevents Content-Type spoofing)</item>
    /// <item>Magic byte validation (detects files disguised as images)</item>
    /// <item>Image dimension validation (max 4096x4096 pixels)</item>
    /// <item>Filename sanitization (prevents path traversal attacks)</item>
    /// </list>
    /// 
    /// <para><strong>Security Features:</strong></para>
    /// <list type="bullet">
    /// <item>Magic byte verification prevents executable files disguised as images</item>
    /// <item>Size limits prevent memory exhaustion attacks</item>
    /// <item>Dimension limits prevent resource exhaustion</item>
    /// <item>Filename sanitization prevents path traversal vulnerabilities</item>
    /// <item>Failed validation attempts are logged for security monitoring</item>
    /// </list>
    /// 
    /// <para><strong>Performance Characteristics:</strong></para>
    /// <list type="bullet">
    /// <item>Processes files in-memory without temporary storage</item>
    /// <item>Uses efficient stream reading for magic byte validation</item>
    /// <item>Lazy image loading only for dimension checking</item>
    /// <item>Early return on validation failures to minimize processing</item>
    /// </list>
    /// 
    /// <para><strong>Error Handling:</strong></para>
    /// All exceptions are caught and converted to user-friendly error messages.
    /// Security-related validation failures are logged for monitoring purposes.
    /// </remarks>
    public async Task<ImageValidationResult> ValidateImageAsync(IFormFile file)
    {
        var result = new ImageValidationResult();

        // ===================================================================
        // Check 1: File existence and size
        // ===================================================================
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

        // ===================================================================
        // Check 2: File extension validation
        // ===================================================================
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
        {
            result.Errors.Add($"File extension '{extension}' is not allowed. Supported formats: jpg, jpeg, png, gif, webp, bmp.");
            return result;
        }

        // ===================================================================
        // Check 3: MIME type validation
        // ===================================================================
        if (!AllowedMimeTypes.Contains(file.ContentType))
        {
            result.Errors.Add($"Content type '{file.ContentType}' is not allowed for images.");
            return result;
        }

        // ===================================================================
        // Check 4: Magic byte validation (file signature)
        // This prevents malicious executables disguised with image extensions
        // ===================================================================
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

            // ===============================================================
            // Check 5: Image dimension validation
            // Prevents memory exhaustion attacks from extremely large images
            // ===============================================================
            if (!TryReadImageDimensions(stream, extension, out var imageWidth, out var imageHeight))
            {
                result.Errors.Add("File is not a valid image or is corrupted.");
                _logger.LogWarning("Failed to read image dimensions from file {FileName}", file.FileName);
                return result;
            }

            if (imageWidth > MaxImageWidth || imageHeight > MaxImageHeight)
            {
                result.Errors.Add($"Image dimensions ({imageWidth}x{imageHeight}) exceed maximum allowed size ({MaxImageWidth}x{MaxImageHeight}).");
                return result;
            }

            result.ImageWidth = imageWidth;
            result.ImageHeight = imageHeight;
        }
        catch (Exception ex)
        {
            result.Errors.Add("An error occurred while validating the file.");
            _logger.LogError(ex, "Unexpected error during image validation for file {FileName}", file.FileName);
            return result;
        }

        // ===================================================================
        // Check 6: Filename validation and sanitization
        // ===================================================================
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
    /// Prevents malicious executables disguised as image files.
    /// </summary>
    /// <param name="fileHeader">The first 8 bytes of the uploaded file content. 
    /// Must contain enough bytes to match the longest magic byte signature (8 bytes for PNG).</param>
    /// <param name="extension">The file extension in lowercase (e.g., ".jpg", ".png"). 
    /// Must match one of the supported image formats defined in MagicBytes dictionary.</param>
    /// <returns>
    /// Returns true if the file header bytes match at least one known magic byte signature 
    /// for the specified extension. Returns false if extension is unsupported or 
    /// file signature doesn't match expected values for the format.
    /// </returns>
    /// <remarks>
    /// <para><strong>Security Purpose:</strong></para>
    /// Magic byte validation is critical for preventing security attacks where malicious
    /// executables are disguised with image file extensions. This method checks the actual
    /// file content signature against known patterns for legitimate image formats.
    /// 
    /// <para><strong>Supported Format Signatures:</strong></para>
    /// <list type="bullet">
    /// <item>JPEG/JPG: FF D8 FF (JPEG File Interchange Format)</item>
    /// <item>PNG: 89 50 4E 47 0D 0A 1A 0A (PNG signature)</item>
    /// <item>GIF: 47 49 46 38 (GIF87a or GIF89a)</item>
    /// <item>WebP: 52 49 46 46 (RIFF container format)</item>
    /// <item>BMP: 42 4D (Bitmap file header)</item>
    /// </list>
    /// 
    /// <para><strong>Validation Logic:</strong></para>
    /// The method supports multiple signatures per format (e.g., GIF87a and GIF89a)
    /// and performs byte-by-byte comparison for exact matching.
    /// </remarks>
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

    private static bool TryReadImageDimensions(Stream stream, string extension, out int width, out int height)
    {
        width = 0;
        height = 0;

        if (!stream.CanSeek)
        {
            return false;
        }

        stream.Position = 0;

        return extension switch
        {
            ".png" => TryReadPngDimensions(stream, out width, out height),
            ".gif" => TryReadGifDimensions(stream, out width, out height),
            ".bmp" => TryReadBmpDimensions(stream, out width, out height),
            ".jpg" or ".jpeg" => TryReadJpegDimensions(stream, out width, out height),
            ".webp" => TryReadWebpDimensions(stream, out width, out height),
            _ => false
        };
    }

    private static bool TryReadPngDimensions(Stream stream, out int width, out int height)
    {
        width = 0;
        height = 0;

        var header = new byte[24];
        if (stream.Read(header, 0, header.Length) < header.Length)
        {
            return false;
        }

        width = ReadInt32BigEndian(header, 16);
        height = ReadInt32BigEndian(header, 20);
        return width > 0 && height > 0;
    }

    private static bool TryReadGifDimensions(Stream stream, out int width, out int height)
    {
        width = 0;
        height = 0;

        var header = new byte[10];
        if (stream.Read(header, 0, header.Length) < header.Length)
        {
            return false;
        }

        width = header[6] | (header[7] << 8);
        height = header[8] | (header[9] << 8);
        return width > 0 && height > 0;
    }

    private static bool TryReadBmpDimensions(Stream stream, out int width, out int height)
    {
        width = 0;
        height = 0;

        var header = new byte[26];
        if (stream.Read(header, 0, header.Length) < header.Length)
        {
            return false;
        }

        width = BitConverter.ToInt32(header, 18);
        height = Math.Abs(BitConverter.ToInt32(header, 22));
        return width > 0 && height > 0;
    }

    private static bool TryReadJpegDimensions(Stream stream, out int width, out int height)
    {
        width = 0;
        height = 0;

        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);

        if (reader.ReadByte() != 0xFF || reader.ReadByte() != 0xD8)
        {
            return false;
        }

        while (stream.Position < stream.Length)
        {
            byte markerPrefix;
            do
            {
                if (stream.Position >= stream.Length)
                {
                    return false;
                }

                markerPrefix = reader.ReadByte();
            }
            while (markerPrefix != 0xFF);

            byte marker;
            do
            {
                if (stream.Position >= stream.Length)
                {
                    return false;
                }

                marker = reader.ReadByte();
            }
            while (marker == 0xFF);

            if (marker == 0xD9 || marker == 0xDA)
            {
                break;
            }

            var segmentLength = ReadUInt16BigEndian(reader);
            if (segmentLength < 2)
            {
                return false;
            }

            if (marker is >= 0xC0 and <= 0xC3 or >= 0xC5 and <= 0xC7 or >= 0xC9 and <= 0xCB or >= 0xCD and <= 0xCF)
            {
                _ = reader.ReadByte();
                height = ReadUInt16BigEndian(reader);
                width = ReadUInt16BigEndian(reader);
                return width > 0 && height > 0;
            }

            stream.Position += segmentLength - 2;
        }

        return false;
    }

    private static bool TryReadWebpDimensions(Stream stream, out int width, out int height)
    {
        width = 0;
        height = 0;

        var header = new byte[30];
        if (stream.Read(header, 0, header.Length) < header.Length)
        {
            return false;
        }

        var chunkType = System.Text.Encoding.ASCII.GetString(header, 12, 4);
        if (chunkType == "VP8 ")
        {
            width = header[26] | ((header[27] & 0x3F) << 8);
            height = header[28] | ((header[29] & 0x3F) << 8);
            return width > 0 && height > 0;
        }

        if (chunkType == "VP8L")
        {
            width = 1 + (((header[21] & 0x3F) << 8) | header[20]);
            height = 1 + (((header[24] & 0x0F) << 10) | (header[23] << 2) | ((header[22] & 0xC0) >> 6));
            return width > 0 && height > 0;
        }

        if (chunkType == "VP8X")
        {
            width = 1 + header[24] + (header[25] << 8) + (header[26] << 16);
            height = 1 + header[27] + (header[28] << 8) + (header[29] << 16);
            return width > 0 && height > 0;
        }

        return false;
    }

    private static int ReadInt32BigEndian(byte[] buffer, int offset)
    {
        return (buffer[offset] << 24) |
               (buffer[offset + 1] << 16) |
               (buffer[offset + 2] << 8) |
               buffer[offset + 3];
    }

    private static ushort ReadUInt16BigEndian(BinaryReader reader)
    {
        var high = reader.ReadByte();
        var low = reader.ReadByte();
        return (ushort)((high << 8) | low);
    }

    /// <summary>
    /// Sanitizes a filename by removing special characters and limiting length.
    /// Prevents directory traversal and filesystem attacks.
    /// </summary>
    /// <param name="fileName">The original filename (without extension) to sanitize.
    /// Can be null, empty, or contain potentially dangerous characters.</param>
    /// <returns>
    /// Returns a sanitized filename containing only safe characters:
    /// <list type="bullet">
    /// <item>Alphanumeric characters (a-z, A-Z, 0-9)</item>
    /// <item>Underscores (_) and hyphens (-)</item>
    /// <item>Maximum length of 100 characters</item>
    /// <item>GUID-based name if input is null/empty or becomes empty after sanitization</item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// <para><strong>Security Features:</strong></para>
    /// <list type="bullet">
    /// <item>Removes path separators (/ \ ..) to prevent directory traversal</item>
    /// <item>Strips special characters that could cause filesystem issues</item>
    /// <item>Converts spaces to underscores for URL compatibility</item>
    /// <item>Enforces maximum length to prevent filesystem limitations</item>
    /// <item>Generates fallback GUID name if sanitization removes all content</item>
    /// </list>
    /// 
    /// <para><strong>Transformation Process:</strong></para>
    /// <list type="number">
    /// <item>Remove directory traversal patterns (/, \, ..)</item>
    /// <item>Keep only alphanumeric, underscore, hyphen, and space characters</item>
    /// <item>Replace spaces with underscores</item>
    /// <item>Generate GUID-based name if result is empty</item>
    /// <item>Truncate to maximum length if necessary</item>
    /// </list>
    /// 
    /// <para><strong>Example Transformations:</strong></para>
    /// <list type="bullet">
    /// <item>"My Photo.jpg" â†’ "My_Photo"</item>
    /// <item>"../../malicious" â†’ "image_[guid]"</item>
    /// <item>"Product@#$%Image" â†’ "ProductImage"</item>
    /// </list>
    /// </remarks>
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
