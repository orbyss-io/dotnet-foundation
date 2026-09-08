using Microsoft.AspNetCore.Http;

namespace Orbyss.Foundation.Authentication;

/// <summary>Writes an authentication-boundary error response.</summary>
public interface IAuthenticationErrorWriter
{
    /// <summary>Writes the response using the consumer-selected representation.</summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="code">The stable authentication error code.</param>
    Task WriteAsync(HttpContext context, int statusCode, string code);
}
