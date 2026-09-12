using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Orbyss.Foundation.WebDefaults;

/// <summary>Establishes safe correlation and browser-security response headers.</summary>
internal sealed partial class CorrelationAndSecurityHeadersMiddleware(RequestDelegate next)
{
    /// <summary>Names the accepted and returned request-correlation header.</summary>
    public const string HeaderName = "X-Correlation-ID";

    /// <summary>Validates correlation input, creates a logging scope, and emits security headers.</summary>
    public async Task InvokeAsync(HttpContext context, ILogger<CorrelationAndSecurityHeadersMiddleware> logger)
    {
        var supplied = context.Request.Headers[HeaderName].ToString();
        var correlationId = CorrelationIdPattern().IsMatch(supplied)
            ? supplied
            : Guid.NewGuid().ToString("N");
        context.TraceIdentifier = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await next(context).ConfigureAwait(false);
        }
    }

    [GeneratedRegex("^[A-Za-z0-9._-]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex CorrelationIdPattern();
}
