using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Orbyss.Foundation.Authentication;

namespace Orbyss.Foundation.Authentication.BffCookie;

/// <summary>Enforces antiforgery validation on BFF-owned unsafe routes.</summary>
internal sealed class AntiforgeryMiddleware(RequestDelegate next)
{
    /// <summary>Validates the current request before invoking the next middleware.</summary>
    public async Task InvokeAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        IAuthenticationErrorWriter errorWriter)
    {
        if (IsUnsafe(context.Request.Method)
            && (context.Request.Path.StartsWithSegments("/api")
                || context.Request.Path.Equals("/bff/logout")))
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
                    "invalid_antiforgery_token").ConfigureAwait(false);
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
