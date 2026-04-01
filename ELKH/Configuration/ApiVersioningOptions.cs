using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;

namespace ELKH.Configuration;

/// <summary>
/// API versioning configuration options for ELKH platform.
/// Supports multiple versioning strategies and provides backward compatibility.
/// </summary>
public class ApiVersioningOptions
{
    /// <summary>
    /// The default API version when none is specified.
    /// </summary>
    public string DefaultVersion { get; set; } = "1.0";

    /// <summary>
    /// Whether to assume the default version when no version is specified.
    /// </summary>
    public bool AssumeDefaultVersionWhenUnspecified { get; set; } = true;

    /// <summary>
    /// Supported API version formats (Header, QueryString, UrlSegment).
    /// </summary>
    public IApiVersionReader VersionReader { get; set; } = ApiVersionReader.Combine(
        new QueryStringApiVersionReader("v"),           // ?v=1.0
        new HeaderApiVersionReader("X-API-Version"),    // X-API-Version: 1.0
        new UrlSegmentApiVersionReader()                // /api/v1/...
    );

    /// <summary>
    /// Supported API versions.
    /// </summary>
    public List<ApiVersion> SupportedVersions { get; set; } = new()
    {
        new ApiVersion(1, 0), // v1.0 - Initial release
        new ApiVersion(1, 1), // v1.1 - Enhanced features
        new ApiVersion(2, 0)  // v2.0 - Breaking changes
    };

    /// <summary>
    /// API versions that are deprecated but still supported.
    /// </summary>
    public List<ApiVersion> DeprecatedVersions { get; set; } = new()
    {
        // Add deprecated versions here as new versions are released
    };

    /// <summary>
    /// Whether to report API versions in response headers.
    /// </summary>
    public bool ReportApiVersions { get; set; } = true;

    /// <summary>
    /// Custom error message for unsupported API versions.
    /// </summary>
    public string UnsupportedVersionMessage { get; set; } = 
        "The requested API version is not supported. Supported versions: {0}";
}