using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orbyss.Foundation.Authentication.Core;
using Orbyss.Foundation.Web.ProblemDetails;

namespace Orbyss.Foundation.Authentication.BffCookie;

/// <summary>Reuses the exact native BFF challenge, validated identity and logout operations.</summary>
internal sealed class BffBrowserAuthenticationSession(
    IValidatedAccountIdentityReader identities, IAuthenticationErrorWriter errors,
    ILoggerFactory logs, IOptions<FoundationWebOptions> options) : IBrowserAuthenticationSession, IProblemPageSessionReader
{
    /// <inheritdoc />
    public bool IsAuthenticated(HttpContext context) => identities.TryRead(context.User, out _);
    /// <inheritdoc />
    public bool? ReadLocalSession(HttpContext context) => context.Features.Get<IAuthenticationFeature>() is null ? null : IsAuthenticated(context);
    /// <inheritdoc />
    public Task SignInAsync(HttpContext context, string returnTarget) =>
        FoundationBffCookieFeature.BrowserLoginAsync(context, returnTarget, errors, logs);
    /// <inheritdoc />
    public Task SignOutAsync(HttpContext context) => FoundationBffCookieFeature.BrowserLogoutAsync(context, logs, options);
}
