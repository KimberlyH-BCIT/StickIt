using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ELKH.Controllers;

/// <summary>
/// Handles unhandled exceptions and HTTP error status codes, rendering a user-friendly
/// error page and logging the originating exception.
/// The route /Error is registered as the exception handler in the middleware pipeline.
/// </summary>
[AllowAnonymous]
public class ErrorController(ILogger<ErrorController> logger) : Controller
{
    /// <summary>
    /// GET /Error  - renders the generic error page.
    /// Logs the originating exception (if any) at Error level.
    /// Never exposes stack traces or internal messages to the client.
    /// </summary>
    /// Never exposes stack traces or internal messages to the client.
    /// </summary>
    [Route("/Error")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Index()
    {
        var feature = HttpContext.Features.Get<IExceptionHandlerFeature>();
        if (feature?.Error is not null)
        {
            logger.LogError(feature.Error,
                "Unhandled exception on {Method} {Path}",
                HttpContext.Request.Method,
                HttpContext.Request.Path);
        }

        ViewBag.RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        return View("Error");
    }

    /// <summary>
    /// GET /Error/{statusCode} - renders a status-code-specific error page
    /// (404 Not Found, 403 Forbidden, etc.).
    /// </summary>
    [Route("/Error/{statusCode:int}")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public new IActionResult StatusCode(int statusCode)
    {
        logger.LogWarning("HTTP {StatusCode} on {Path}", statusCode, HttpContext.Request.Path);
        ViewBag.StatusCode = statusCode;
        ViewBag.RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        return View("Error");
    }
}
