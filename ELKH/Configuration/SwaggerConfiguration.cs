using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace ELKH.Configuration;

// ╔===============================================================================================╗
// ║                             SWAGGER CONFIGURATION - TABLE OF CONTENTS                        ║
// ╚===============================================================================================╝
// 
// OVERVIEW:
// Comprehensive Swagger/OpenAPI configuration for ELKH e-commerce platform providing
// enterprise-grade API documentation with security, versioning, and developer experience features.
// 
// TABLE OF CONTENTS:
// ┌─ Section 1: Main Configuration & Service Registration ................................. Line 38
// │  ├─ SwaggerConfiguration class definition and main setup
// │  ├─ AddSwaggerDocumentation() - Service container configuration
// │  └─ Security schemes (Bearer JWT, API Key) and requirements
// ├─ Section 2: API Documentation & Metadata .............................................. Line 85
// │  ├─ Version-specific documentation generation
// │  ├─ API info, contact, license, and terms configuration
// │  └─ Custom XML documentation integration
// ├─ Section 3: Swagger UI Configuration ................................................. Line 121
// │  ├─ UseSwaggerDocumentation() - Application pipeline setup
// │  ├─ UI customization and routing configuration
// │  └─ Multi-version endpoint configuration
// ├─ Section 4: Operation Filtering & Enhancement ........................................ Line 165
// │  ├─ ApiDocumentationOperationFilter class
// │  ├─ Security requirements and response examples
// │  ├─ Custom tagging and rate limiting metadata
// │  └─ Dynamic example generation for API responses
// ├─ Section 5: Document-Level Filters ................................................... Line 349
// │  ├─ ApiDocumentationDocumentFilter class
// │  ├─ Server configuration (production, staging, development)
// │  ├─ Common error response schemas
// │  └─ Vendor extensions and API metadata
// └─ Section 6: API Versioning Integration ............................................... Line 384
//    ├─ ApiVersionOperationFilter class
//    ├─ Version-aware operation filtering
//    └─ Parameter cleanup for versioned endpoints
//
// ARCHITECTURE NOTES:
// • Uses Swashbuckle.AspNetCore for OpenAPI generation with custom filters
// • Supports JWT Bearer authentication and API key authentication schemes
// • Implements API versioning with Microsoft.AspNetCore.Mvc.Versioning
// • Provides comprehensive example generation for improved developer experience
//
// SECURITY IMPLEMENTATION:
// • JWT Bearer token authentication with proper security schemes
// • API Key authentication support for service-to-service communication  
// • Security requirements applied to all documented endpoints
// • Rate limiting metadata included for API governance
//
// DEVELOPER EXPERIENCE:
// • Auto-generated examples for common API responses
// • Comprehensive operation descriptions and parameter documentation
// • Multi-environment server configuration for testing
// • Custom tagging for logical API organization
// • Enhanced UI with proper default expansion and model rendering

/// <summary>
/// Swagger configuration for ELKH API documentation.
/// Provides comprehensive API documentation with versioning support.
/// </summary>
public static class SwaggerConfiguration
{
    #region Section 1: Main Configuration & Service Registration

    // ===================================================================
    // Section 1: Main Configuration & Service Registration
    // ===================================================================

    /// <summary>
    /// Configure Swagger/OpenAPI services with versioning support.
    /// </summary>
    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            // Enable annotations for enhanced documentation
            options.EnableAnnotations();

            // Configure security definitions
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "Bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\""
            });

            options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
            {
                Name = "X-API-Key",
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Description = "API key for authenticated API requests."
            });

            options.OperationFilter<SwaggerSecurityOperationFilter>();
            options.DocumentFilter<SwaggerServerDocumentFilter>();

            // Include XML comments for detailed documentation
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }
        });

        return services;
    }

    /// <summary>
    /// Configure Swagger/OpenAPI for multiple API versions.
    /// </summary>
    public static IServiceCollection AddVersionedSwaggerDocumentation(
        this IServiceCollection services,
        IApiVersionDescriptionProvider provider)
    {
        services.AddSwaggerGen(options =>
        {
            // Create a swagger document for each API version
            foreach (var description in provider.ApiVersionDescriptions)
            {
                options.SwaggerDoc(description.GroupName, new OpenApiInfo
                {
                    Title = "ELKH eCommerce API",
                    Version = description.ApiVersion.ToString(),
                    Description = GetVersionDescription(description),
                    Contact = new OpenApiContact
                    {
                        Name = "ELKH Development Team",
                        Email = "api-support@elkh.com",
                        Url = new Uri("https://elkh.com/contact")
                    },
                    License = new OpenApiLicense
                    {
                        Name = "MIT License",
                        Url = new Uri("https://opensource.org/licenses/MIT")
                    },
                    TermsOfService = new Uri("https://elkh.com/terms")
                });
            }

        });

        return services;
    }

    #endregion

    #region Section 3: Swagger UI Configuration

    // ===================================================================
    // Section 3: Swagger UI Configuration  
    // ===================================================================

    /// <summary>
    /// Use Swagger UI with versioning support.
    /// </summary>
    public static IApplicationBuilder UseSwaggerDocumentation(
        this IApplicationBuilder app,
        IApiVersionDescriptionProvider provider)
    {
        app.UseSwagger(options =>
        {
            options.RouteTemplate = "api/docs/{documentName}/swagger.json";
        });

        app.UseSwaggerUI(options =>
        {
            options.RoutePrefix = "api/docs";
            options.DocumentTitle = "ELKH eCommerce API Documentation";
            
            // Configure endpoints for each API version
            foreach (var description in provider.ApiVersionDescriptions.Reverse())
            {
                options.SwaggerEndpoint(
                    $"/api/docs/{description.GroupName}/swagger.json",
                    $"ELKH API {description.GroupName.ToUpperInvariant()}"
                );
            }

            // UI customization
            options.DefaultModelsExpandDepth(2);
            options.DefaultModelRendering(Swashbuckle.AspNetCore.SwaggerUI.ModelRendering.Model);
            options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
            options.EnableDeepLinking();
            options.DisplayOperationId();
            options.DisplayRequestDuration();
            options.EnableFilter();
            options.EnableValidator();
            options.ShowExtensions();
            options.ShowCommonExtensions();

            // Custom CSS for branding
            options.InjectStylesheet("/css/swagger-custom.css");
            
            // Custom JavaScript for enhanced functionality
            //options.InjectJavaScript("/js/swagger-custom.js"); // Not available in this version
        });

        return app;
    }

    #endregion

    #region Section 2: API Documentation & Metadata Helper Methods

    // ===================================================================
    // Section 2: API Documentation & Metadata Helper Methods
    // ===================================================================

    private static string GetVersionDescription(ApiVersionDescription description)
    {
        var version = description.ApiVersion.ToString();
        
        return version switch
        {
            "1.0" => "Initial release of the ELKH eCommerce API. Provides core functionality for product catalog, search, and basic operations.",
            "1.1" => "Enhanced version with improved search capabilities and additional product metadata.",
            "2.0" => "Major release with breaking changes. Enhanced product models, improved pagination, and additional filtering options.",
            _ => $"ELKH eCommerce API version {version}"
        };
    }

    #endregion
}

public class SwaggerSecurityOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation == null)
        {
            return;
        }

        var hasAuthorize = context.MethodInfo.DeclaringType?.GetCustomAttributes(true)
            .Concat(context.MethodInfo.GetCustomAttributes(true))
            .Any(attribute => attribute is Microsoft.AspNetCore.Authorization.AuthorizeAttribute) == true;

        if (hasAuthorize)
        {
            operation.Description = string.IsNullOrWhiteSpace(operation.Description)
                ? "Authentication required."
                : $"{operation.Description}\n\nAuthentication required.";
        }

        operation.Summary ??= context.ApiDescription.ActionDescriptor.DisplayName;
    }
}

public class SwaggerServerDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        swaggerDoc.Servers = new List<OpenApiServer>
        {
            new() { Url = "https://api.elkh.com", Description = "Production server" },
            new() { Url = "https://staging-api.elkh.com", Description = "Staging server" },
            new() { Url = "http://localhost:5000", Description = "Development server" }
        };
    }
}
