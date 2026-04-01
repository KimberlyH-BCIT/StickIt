using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELKH.Models;

/// <summary>
/// Entity model representing product images stored in the database.
/// Handles binary image data storage with metadata for the ELKH e-commerce platform.
/// </summary>
/// <remarks>
/// This model represents images associated with products in the ELKH store.
/// Images are stored as binary data (BLOB) in the database with associated
/// metadata including filename, description, and file type information.
/// 
/// <para><strong>Storage Strategy:</strong></para>
/// Images are stored directly in the database as binary data rather than
/// as file system references. This approach provides:
/// <list type="bullet">
/// <item>Transactional consistency with product data</item>
/// <item>Simplified backup and deployment processes</item>
/// <item>Better security control over image access</item>
/// <item>No broken file references due to file system changes</item>
/// </list>
/// 
/// <para><strong>Security Considerations:</strong></para>
/// <list type="bullet">
/// <item>File type validation prevents malicious file uploads</item>
/// <item>Binary data storage prevents direct file system access</item>
/// <item>Access control managed through application logic</item>
/// <item>Image serving through controlled endpoints only</item>
/// </list>
/// 
/// <para><strong>Performance Considerations:</strong></para>
/// While database storage simplifies management, consider:
/// <list type="bullet">
/// <item>Database size growth with image volume</item>
/// <item>Memory usage when serving large images</item>
/// <item>Caching strategies for frequently accessed images</item>
/// <item>CDN integration for high-traffic scenarios</item>
/// </list>
/// 
/// <para><strong>Related Entities:</strong></para>
/// <list type="bullet">
/// <item>FkProductId links to ProductModel entities</item>
/// <item>No navigation property to maintain context isolation</item>
/// <item>ProductImageURL provides alternative access path</item>
/// </list>
/// </remarks>
public partial class ImageModel
{
    /// <summary>
    /// Primary key identifier for the image record.
    /// </summary>
    /// <remarks>
    /// Auto-generated integer primary key providing unique identification
    /// for each image stored in the system.
    /// </remarks>
    [Key]
    public int ImageId { get; set; }

    /// <summary>
    /// Original filename of the uploaded image.
    /// </summary>
    /// <remarks>
    /// Preserves the original filename for user recognition and download purposes.
    /// May be sanitized for security but maintains user-friendly naming.
    /// </remarks>
    public string FileName { get; set; } = null!;

    /// <summary>
    /// Descriptive text for the image content.
    /// </summary>
    /// <remarks>
    /// User-provided or system-generated description of the image.
    /// Used for accessibility (alt text) and search functionality.
    /// Supports SEO optimization and screen reader compatibility.
    /// </remarks>
    public string Description { get; set; } = null!;

    /// <summary>
    /// MIME type of the image file.
    /// </summary>
    /// <remarks>
    /// Specifies the image format (e.g., "image/jpeg", "image/png", "image/webp").
    /// Used for proper HTTP content type headers when serving images.
    /// Validated against allowed image types for security.
    /// </remarks>
    public string FileType { get; set; } = null!;

    /// <summary>
    /// Binary data content of the image.
    /// </summary>
    /// <remarks>
    /// Raw image data stored as byte array in the database.
    /// Supports all common image formats (JPEG, PNG, GIF, WebP).
    /// Size limitations should be enforced at the application level.
    /// </remarks>
    public byte[] ImageData { get; set; } = null!;

    /// <summary>
    /// Foreign key reference to the associated product.
    /// </summary>
    /// <remarks>
    /// Links this image to a specific product in the catalog.
    /// No navigation property is included to maintain context isolation
    /// between the image storage context and main application context.
    /// </remarks>
    public int FkProductId { get; set; }

    /// <summary>
    /// URL path for accessing the image via HTTP.
    /// </summary>
    /// <remarks>
    /// Provides a web-accessible URL for the image, typically pointing
    /// to a controller action that serves the image data with proper
    /// HTTP headers and caching controls.
    /// Example: "/Images/Product/123" or "/api/images/456"
    /// </remarks>
    public string ProductImageURL { get; set; } = null!;
}
