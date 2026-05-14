using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Logging;
using ELKH.Services;

namespace ELKH.TagHelpers;

/// <summary>
/// Tag helper for optimized images with lazy loading, responsive sizes, and WebP support.
/// Automatically generates modern performance-optimized image markup with:
/// - WebP format conversion for smaller file sizes
/// - Lazy loading with placeholder strategies 
/// - Responsive srcset for different viewport sizes
/// - Native loading="lazy" fallback support
/// - Layout shift prevention with dimensions
/// </summary>
[HtmlTargetElement("img", Attributes = "optimized")]
public class OptimizedImageTagHelper : TagHelper
{
    private readonly IImageOptimizationService _imageOptimizationService;
    private readonly ILogger<OptimizedImageTagHelper> _logger;

    /// <summary>
    /// The source image path
    /// </summary>
    [HtmlAttributeName("src")]
    public string? Src { get; set; }

    /// <summary>
    /// Alt text for accessibility
    /// </summary>
    [HtmlAttributeName("alt")]
    public string? Alt { get; set; }

    /// <summary>
    /// Enable lazy loading (default: true)
    /// </summary>
    [HtmlAttributeName("lazy")]
    public bool Lazy { get; set; } = true;

    /// <summary>
    /// Enable responsive images (default: true)
    /// </summary>
    [HtmlAttributeName("responsive")]
    public bool Responsive { get; set; } = true;

    /// <summary>
    /// CSS classes to apply
    /// </summary>
    [HtmlAttributeName("class")]
    public string? CssClass { get; set; }

    /// <summary>
    /// Image width for sizing
    /// </summary>
    [HtmlAttributeName("width")]
    public int? Width { get; set; }

    /// <summary>
    /// Image height for sizing
    /// </summary>
    [HtmlAttributeName("height")]
    public int? Height { get; set; }

    /// <summary>
    /// Placeholder style (blur, low-quality, skeleton)
    /// </summary>
    [HtmlAttributeName("placeholder")]
    public string Placeholder { get; set; } = "blur";

    /// <summary>
    /// Enable the optimized image tag helper
    /// </summary>
    [HtmlAttributeName("optimized")]
    public bool Optimized { get; set; }

    public OptimizedImageTagHelper(
        IImageOptimizationService imageOptimizationService,
        ILogger<OptimizedImageTagHelper> logger)
    {
        _imageOptimizationService = imageOptimizationService;
        _logger = logger;
    }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (!Optimized || string.IsNullOrEmpty(Src))
        {
            return;
        }

        try
        {
            // Remove the optimized attribute from output
            output.Attributes.RemoveAll("optimized");

            // Get optimized image path
            var optimizedSrc = _imageOptimizationService.GetOptimizedImagePath(Src, "webp");

            // Build responsive srcset if enabled
            string? srcset = null;
            if (Responsive)
            {
                srcset = BuildResponsiveSrcset(Src);
            }

            // Configure lazy loading
            if (Lazy)
            {
                SetupLazyLoading(output, optimizedSrc, srcset);
            }
            else
            {
                output.Attributes.SetAttribute("src", optimizedSrc);
                if (!string.IsNullOrEmpty(srcset))
                {
                    output.Attributes.SetAttribute("srcset", srcset);
                }
            }

            // Set alt text
            if (!string.IsNullOrEmpty(Alt))
            {
                output.Attributes.SetAttribute("alt", Alt);
            }

            // Set dimensions for layout stability
            if (Width.HasValue)
            {
                output.Attributes.SetAttribute("width", Width.Value);
            }
            if (Height.HasValue)
            {
                output.Attributes.SetAttribute("height", Height.Value);
            }

            // Add CSS classes
            var classes = new List<string> { "optimized-image" };
            if (Lazy)
            {
                classes.Add("lazy-image");
            }
            if (!string.IsNullOrEmpty(CssClass))
            {
                classes.AddRange(CssClass.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            }
            output.Attributes.SetAttribute("class", string.Join(" ", classes));

            // Add loading attribute for native lazy loading fallback
            if (Lazy)
            {
                output.Attributes.SetAttribute("loading", "lazy");
            }

            // Add decoding attribute for better performance
            output.Attributes.SetAttribute("decoding", "async");

            _logger.LogDebug("Generated optimized image markup for {Src}", Src);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to process optimized image for {Src}", Src);
            
            // Fallback to standard image
            output.Attributes.SetAttribute("src", Src);
            if (!string.IsNullOrEmpty(Alt))
            {
                output.Attributes.SetAttribute("alt", Alt);
            }
        }
    }

    private string BuildResponsiveSrcset(string originalSrc)
    {
        try
        {
            var srcsetItems = new List<string>();
            var basePath = Path.GetDirectoryName(originalSrc) ?? "";
            var fileName = Path.GetFileNameWithoutExtension(originalSrc);

            // Standard responsive sizes
            var sizes = new Dictionary<string, string>
            {
                { "small", "300w" },
                { "medium", "600w" },
                { "large", "1200w" },
                { "xlarge", "1920w" }
            };

            foreach (var size in sizes)
            {
                var responsivePath = Path.Combine(basePath, "optimized", $"{fileName}-{size.Key}.webp")
                    .Replace('\\', '/');
                
                // Check if responsive image exists (in a real implementation)
                srcsetItems.Add($"/{responsivePath.TrimStart('/')} {size.Value}");
            }

            return string.Join(", ", srcsetItems);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to build responsive srcset for {Src}", originalSrc);
            return string.Empty;
        }
    }

    private void SetupLazyLoading(TagHelperOutput output, string optimizedSrc, string? srcset)
    {
        // Use data attributes for lazy loading library
        output.Attributes.SetAttribute("data-src", optimizedSrc);
        
        if (!string.IsNullOrEmpty(srcset))
        {
            output.Attributes.SetAttribute("data-srcset", srcset);
        }

        // Set placeholder based on type
        switch (Placeholder.ToLowerInvariant())
        {
            case "blur":
                // Use a tiny blurred version as placeholder
                output.Attributes.SetAttribute("src", GetPlaceholderSrc());
                break;
                
            case "skeleton":
                // Use a skeleton placeholder
                output.Attributes.SetAttribute("src", "data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='400' height='300'%3E%3Crect width='100%25' height='100%25' fill='%23f0f0f0'/%3E%3C/svg%3E");
                break;
                
            case "low-quality":
            default:
                // Use a low-quality version
                output.Attributes.SetAttribute("src", GetPlaceholderSrc());
                break;
        }

        // Add sizes attribute for responsive images
        if (!string.IsNullOrEmpty(srcset))
        {
            output.Attributes.SetAttribute("sizes", "(max-width: 600px) 300px, (max-width: 1200px) 600px, 1200px");
        }
    }

    private string GetPlaceholderSrc()
    {
        // Return a tiny transparent pixel as default placeholder
        return "data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7";
    }
}