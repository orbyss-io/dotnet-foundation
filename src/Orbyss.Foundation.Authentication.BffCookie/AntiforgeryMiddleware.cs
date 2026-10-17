using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Orbyss.Foundation.Authentication.Core;
using Orbyss.Foundation.Authentication;
using Orbyss.Foundation.WebDefaults;
using CShells;

namespace Orbyss.Foundation.Authentication.BffCookie;

/// <summary>Enforces antiforgery validation on BFF-owned unsafe routes.</summary>
internal sealed class AntiforgeryMiddleware(RequestDelegate next)
{
    /// <summary>Validates the current request before invoking the next middleware.</summary>
    public async Task InvokeAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        IAuthenticationErrorWriter errorWriter,
        ShellSettings settings)
    {
        var path = context.Request.Path;
        var prefix = ShellWebPaths.RoutePrefix(settings);
        if (prefix.Length > 0 && path.StartsWithSegments(prefix, out var remainder)) path = remainder;
        if (IsUnsafe(context.Request.Method)
            && (path.StartsWithSegments("/api") || path.Equals("/bff/logout")))
        {
            try
            {
                await antiforgery.ValidateRequestAsync(context).ConfigureAwait(false);
            }
            catch (AntiforgeryValidationException)
            {
                await errorWriter.WriteAsync(
                    context,
                    StatusCodes.Status400BadRequest,
                    AuthenticationErrorCodes.InvalidAntiforgeryToken).ConfigureAwait(false);
                return;
            }
        }

        await next(context).ConfigureAwait(false);
    }

    /// <summary>Returns whether an HTTP method can mutate server state.</summary>
    private static bool IsUnsafe(string method) => !HttpMethods.IsGet(method)
        && !HttpMethods.IsHead(method)
        && !HttpMethods.IsOptions(method)
        && !HttpMethods.IsTrace(method);
}
