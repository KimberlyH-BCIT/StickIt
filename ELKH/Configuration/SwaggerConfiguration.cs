using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Any;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace ELKH.Configuration;

/// <summary>
/// Swagger configuration for ELKH API documentation.
/// Provides comprehensive API documentation with versioning support.
/// </summary>
public static class SwaggerConfiguration
{
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
                Description = "API Key for authentication. Example: \"X-API-Key: your-api-key\""
            });

            // Add security requirement for all endpoints
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });

            // Include XML comments for detailed documentation
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }

            // Custom operation filter for additional metadata
            options.OperationFilter<SwaggerOperationFilter>();
            
            // Schema filter for custom model documentation
            options.SchemaFilter<SwaggerSchemaFilter>();

            // Document filter for additional API information
            options.DocumentFilter<SwaggerDocumentFilter>();
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

            // Version-specific documentation
            options.OperationFilter<ApiVersionOperationFilter>();
        });

        return services;
    }

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
}

/// <summary>
/// Operation filter to add custom metadata to Swagger operations.
/// </summary>
public class SwaggerOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Add response examples
        if (operation.Responses.ContainsKey("200"))
        {
            var response = operation.Responses["200"];
            if (response.Content.ContainsKey("application/json"))
            {
                var mediaType = response.Content["application/json"];
                // Add example responses based on the operation
                AddExampleResponses(operation, mediaType, context);
            }
        }

        // Add custom tags based on controller
        var controllerName = context.MethodInfo.DeclaringType?.Name;
        if (controllerName != null)
        {
            if (controllerName.Contains("Product"))
                operation.Tags = new List<OpenApiTag> { new() { Name = "Products" } };
            else if (controllerName.Contains("Cart"))
                operation.Tags = new List<OpenApiTag> { new() { Name = "Shopping Cart" } };
            else if (controllerName.Contains("User"))
                operation.Tags = new List<OpenApiTag> { new() { Name = "User Management" } };
        }

        // Add rate limiting information
        operation.Extensions.Add("x-rate-limit", new OpenApiObject
        {
            ["requests"] = new OpenApiInteger(100),
            ["period"] = new OpenApiString("minute")
        });
    }

    private void AddExampleResponses(OpenApiOperation operation, OpenApiMediaType mediaType, OperationFilterContext context)
    {
        var methodName = context.MethodInfo.Name;
        var controllerName = context.MethodInfo.DeclaringType?.Name;

        // Add examples based on the endpoint
        if (controllerName?.Contains("ProductApi") == true)
        {
            if (methodName == "GetProducts")
            {
                mediaType.Example = CreateProductListExample();
            }
            else if (methodName == "GetProduct")
            {
                mediaType.Example = CreateSingleProductExample();
            }
        }
    }

    private OpenApiObject CreateProductListExample()
    {
        return new OpenApiObject
        {
            ["data"] = new OpenApiObject
            {
                ["items"] = new OpenApiArray
                {
                    new OpenApiObject
                    {
                        ["id"] = new OpenApiInteger(1),
                        ["name"] = new OpenApiString("Funny Cat Sticker"),
                        ["description"] = new OpenApiString("Hilarious cat sticker for laptops"),
                        ["price"] = new OpenApiDouble(9.99),
                        ["discountPercent"] = new OpenApiDouble(0),
                        ["stockQuantity"] = new OpenApiInteger(50),
                        ["isInStock"] = new OpenApiBoolean(true)
                    }
                },
                ["page"] = new OpenApiInteger(1),
                ["pageSize"] = new OpenApiInteger(20),
                ["totalCount"] = new OpenApiInteger(100),
                ["totalPages"] = new OpenApiInteger(5)
            },
            ["success"] = new OpenApiBoolean(true),
            ["message"] = new OpenApiString("Products retrieved successfully")
        };
    }

    private OpenApiObject CreateSingleProductExample()
    {
        return new OpenApiObject
        {
            ["data"] = new OpenApiObject
            {
                ["id"] = new OpenApiInteger(1),
                ["name"] = new OpenApiString("Funny Cat Sticker"),
                ["description"] = new OpenApiString("Hilarious cat sticker for laptops"),
                ["price"] = new OpenApiDouble(9.99),
                ["discountPercent"] = new OpenApiDouble(10),
                ["stockQuantity"] = new OpenApiInteger(50),
                ["isInStock"] = new OpenApiBoolean(true),
                ["categoryId"] = new OpenApiInteger(1),
                ["isActive"] = new OpenApiBoolean(true)
            },
            ["success"] = new OpenApiBoolean(true),
            ["message"] = new OpenApiString("Product retrieved successfully")
        };
    }
}

/// <summary>
/// Schema filter to add custom model documentation.
/// </summary>
public class SwaggerSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        // Add examples for common models
        if (context.Type.Name.Contains("ProductApi"))
        {
            AddProductSchemaExamples(schema, context);
        }

        // Add validation information to schema
        AddValidationInfo(schema, context);
    }

    private void AddProductSchemaExamples(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema.Properties?.ContainsKey("name") == true)
        {
            schema.Properties["name"].Example = new OpenApiString("Awesome Sticker");
        }

        if (schema.Properties?.ContainsKey("price") == true)
        {
            schema.Properties["price"].Example = new OpenApiDouble(9.99);
        }
    }

    private void AddValidationInfo(OpenApiSchema schema, SchemaFilterContext context)
    {
        // Add format information for common types
        if (schema.Properties != null)
        {
            foreach (var property in schema.Properties)
            {
                if (property.Key.ToLower().Contains("email"))
                {
                    property.Value.Format = "email";
                }
                else if (property.Key.ToLower().Contains("url"))
                {
                    property.Value.Format = "uri";
                }
                else if (property.Key.ToLower().Contains("date"))
                {
                    property.Value.Format = "date-time";
                }
            }
        }
    }
}

/// <summary>
/// Document filter to add additional API information.
/// </summary>
public class SwaggerDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        // Add custom vendor extensions
        swaggerDoc.Extensions.Add("x-api-id", new OpenApiString("elkh-ecommerce-api"));
        swaggerDoc.Extensions.Add("x-audience", new OpenApiString("public"));
        
        // Add servers information
        swaggerDoc.Servers = new List<OpenApiServer>
        {
            new() { Url = "https://api.elkh.com", Description = "Production server" },
            new() { Url = "https://staging-api.elkh.com", Description = "Staging server" },
            new() { Url = "http://localhost:5000", Description = "Development server" }
        };

        // Add common response schemas
        AddCommonSchemas(swaggerDoc);
    }

    private void AddCommonSchemas(OpenApiDocument swaggerDoc)
    {
        swaggerDoc.Components.Schemas.Add("ErrorResponse", new OpenApiSchema
        {
            Type = "object",
            Properties = new Dictionary<string, OpenApiSchema>
            {
                ["success"] = new() { Type = "boolean", Example = new OpenApiBoolean(false) },
                ["message"] = new() { Type = "string", Example = new OpenApiString("Error message") },
                ["errorCode"] = new() { Type = "string", Example = new OpenApiString("ERROR_CODE") }
            }
        });
    }
}

/// <summary>
/// Operation filter to handle API versioning in Swagger.
/// </summary>
public class ApiVersionOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var apiDescription = context.ApiDescription;

        // Check if API is deprecated (simple version check)
        if (apiDescription.ActionDescriptor.EndpointMetadata
            .Any(m => m.GetType().Name.Contains("ApiVersionAttribute")))
        {
            // For now, we'll keep this simple
            operation.Deprecated = false;
        }

        if (operation.Parameters == null)
            return;

        // Remove version parameter from documentation (it's in the URL)
        foreach (var parameter in operation.Parameters.ToList())
        {
            if (parameter.Name == "version")
            {
                operation.Parameters.Remove(parameter);
            }
        }
    }
}