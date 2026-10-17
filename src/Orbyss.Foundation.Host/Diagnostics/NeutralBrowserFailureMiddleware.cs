using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Orbyss.Foundation.Host.Diagnostics;

/// <summary>Provides bounded neutral recovery for native document failures before any endpoint is selected.</summary>
public sealed class NeutralBrowserFailureMiddleware(RequestDelegate next, ILogger<NeutralBrowserFailureMiddleware> logger)
{
    /// <summary>Preserves selected endpoint representations, cancellation and already committed responses.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context).ConfigureAwait(false); }
        catch (Exception error) when (!context.RequestAborted.IsCancellationRequested && !context.Response.HasStarted && Eligible(context))
        {
            logger.LogError("Unselected browser request failed with {FailureType}.", error.GetType().Name);
            context.Response.Clear();
            context.Response.StatusCode = 500;
        }
        if (!Eligible(context) || context.Response.HasStarted || context.Response.StatusCode < 400
            || context.Response.ContentLength is > 0 || !string.IsNullOrEmpty(context.Response.ContentType)) return;
        context.RequestAborted.ThrowIfCancellationRequested();
        var title = context.Response.StatusCode == 404 ? "Page not found" : "Request could not be completed";
        var bytes = Encoding.UTF8.GetBytes("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">"
            + "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>" + title
            + "</title></head><body><main><h1 tabindex=\"-1\" autofocus>" + title
            + "</h1><p>Check the application address or try again later.</p></main></body></html>");
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength = bytes.Length;
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; base-uri 'none'; frame-ancestors 'none'; object-src 'none'; form-action 'none'";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        if (!HttpMethods.IsHead(context.Request.Method)) await context.Response.Body.WriteAsync(bytes, context.RequestAborted).ConfigureAwait(false);
    }
    /// <summary>Requires native document-navigation headers and explicit HTML preference; Accept alone cannot alter an endpoint.</summary>
    internal static bool Eligible(HttpContext context)
    {
        if (context.GetEndpoint() is not null || !(HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
            || context.Request.Headers["Sec-Fetch-Mode"] != "navigate" || context.Request.Headers["Sec-Fetch-Dest"] != "document") return false;
        try
        {
            var accepted = context.Request.GetTypedHeaders().Accept;
            var html = accepted?.Where(item => item.MediaType.Equals("text/html", StringComparison.OrdinalIgnoreCase))
                .Select(item => item.Quality ?? 1).DefaultIfEmpty(0).Max() ?? 0;
            var json = accepted?.Where(item => item.MediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
                    || item.MediaType.Equals("application/problem+json", StringComparison.OrdinalIgnoreCase))
                .Select(item => item.Quality ?? 1).DefaultIfEmpty(0).Max() ?? 0;
            return html > 0 && html >= json;
        }
        catch (FormatException) { return false; }
    }
}
