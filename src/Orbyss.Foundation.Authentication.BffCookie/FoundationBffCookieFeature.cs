using System.Security.Claims;
using CShells.AspNetCore.Features;
using CShells.Features;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Orbyss.Foundation.Authentication;
using Orbyss.Foundation.Authentication.Core;
using Orbyss.Foundation.WebDefaults;
using Orbyss.Foundation.Json;
using Orbyss.Foundation.Json.AspNetCore;
using Orbyss.Foundation.Web.ProblemDetails;
using Orbyss.Foundation.Web.ProblemDetails.Core;

namespace Orbyss.Foundation.Authentication.BffCookie;

/// <summary>Composes the confidential OIDC BFF-cookie profile into one shell.</summary>
[ShellFeature(
    name: "Orbyss.Foundation.Authentication.BffCookie",
    DisplayName = "Orbyss Foundation BFF Cookie Authentication",
    Description = "Provides server-held OIDC tokens, an opaque session cookie, antiforgery, and BFF endpoints.",
    DependsOn = [typeof(FoundationAuthenticationFeature), typeof(FoundationWebDefaultsFeature), typeof(FoundationJsonFeature)])]
public sealed class FoundationBffCookieFeature : IWebShellFeature, IMiddlewareShellFeature
{
    /// <summary>Names the accepted antiforgery header.</summary>
    public const string AntiforgeryHeader = "X-CSRF-TOKEN";

    /// <summary>Names the accepted antiforgery form field.</summary>
    public const string AntiforgeryFormField = "__RequestVerificationToken";

    /// <inheritdoc />
    public int Order => -800;

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        var responseProfile = new JsonProfileKey(JsonProfileKeys.SuccessResponse);
        var responseRequirement = new JsonProfileRequirement(JsonProfileKeys.TolerantResponse);
        services.AddJsonResponseContract<BffUserResponse>(responseProfile, responseRequirement);
        services.AddJsonResponseContract<BffAntiforgeryResponse>(responseProfile, responseRequirement);
        services.AddJsonResponseContract<BffSignedOutResponse>(responseProfile, responseRequirement);
        services.AddSingleton<IFoundationAuthenticationProfile, BffCookieProfileMarker>();
        services.AddSingleton<IValidateOptions<FoundationWebOptions>, BffCookieOptionsValidator>();
        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultForbidScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(_ => { })
            .AddOpenIdConnect(_ => { });
        services.AddDistributedMemoryCache();
        services.AddSingleton<ITicketStore, DistributedTicketStore>();
        ConfigureCookie(services);
        ConfigureOpenIdConnect(services);
        ConfigureAntiforgery(services);
    }

    /// <inheritdoc />
    public void UseMiddleware(IApplicationBuilder app, IHostEnvironment? environment)
    {
        app.UseAuthentication();
        app.UseMiddleware<AntiforgeryMiddleware>();
        app.UseAuthorization();
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment)
    {
        var selected = endpoints.ServiceProvider.GetRequiredService<IOptions<FoundationWebOptions>>().Value;
        // Shell selection happens before its authentication middleware. These exact routes
        // bring protocol requests into the owning shell; the OIDC handler consumes them.
        foreach (var path in new[] { selected.CallbackPath, selected.SignedOutCallbackPath, selected.RemoteSignOutPath })
        {
            endpoints.MapMethods(path, ["GET", "POST"], (HttpContext context, IAuthenticationErrorWriter errorWriter) =>
                errorWriter.WriteAsync(context, StatusCodes.Status400BadRequest, AuthenticationErrorCodes.CallbackInvalid))
                .WithMetadata(new WebResponseMetadata(Private: true))
                .AllowAnonymous()
                .ExcludeFromDescription();
        }
        endpoints.MapGet("/bff/login", (string? returnUrl) =>
        {
            var destination = IsLocalReturnUrl(returnUrl) ? returnUrl! : "/";
            return Results.Challenge(
                new AuthenticationProperties { RedirectUri = destination },
                [OpenIdConnectDefaults.AuthenticationScheme]);
        }).WithMetadata(new WebResponseMetadata(Private: true)).AllowAnonymous();

        endpoints.MapGet("/bff/user", WriteUserAsync).WithJsonResponse<BffUserResponse>().WithMetadata(new WebResponseMetadata(Private: true)).AllowAnonymous();

        endpoints.MapGet("/bff/antiforgery", (HttpContext context, IAntiforgery antiforgery, IJsonResponseFactory<BffAntiforgeryResponse> responses) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(context);
            return responses.Create(new(AntiforgeryHeader, AntiforgeryFormField, tokens.RequestToken));
        }).WithJsonResponse<BffAntiforgeryResponse>().WithMetadata(new WebResponseMetadata(Private: true)).AllowAnonymous();

        endpoints.MapPost("/bff/logout", LogoutAsync).WithMetadata(new WebResponseMetadata(Private: true)).RequireAuthorization();
        endpoints.MapGet("/bff/signed-out", (IJsonResponseFactory<BffSignedOutResponse> responses) => responses.Create(new(true)))
            .WithJsonResponse<BffSignedOutResponse>().WithMetadata(new WebResponseMetadata(Private: true)).AllowAnonymous();
        endpoints.MapGet(selected.AccessDeniedPath, WriteAccessDeniedAsync).WithMetadata(new WebResponseMetadata(Private: true)).AllowAnonymous();
    }

    /// <summary>Configures the opaque server-backed session cookie.</summary>
    private static void ConfigureCookie(IServiceCollection services)
    {
        services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
            .Configure<IOptions<FoundationWebOptions>, IHostEnvironment, ITicketStore>(
                (options, selected, environment, store) =>
                {
                    var settings = selected.Value;
                    var insecureLocal = environment.IsDevelopment() && settings.AllowHttpForLocalDevelopment;
                    options.SessionStore = store;
                    options.Cookie.Name = insecureLocal ? "orbyss-foundation-session-local" : settings.CookieName;
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SameSite = SameSiteMode.Lax;
                    options.Cookie.SecurePolicy = insecureLocal
                        ? CookieSecurePolicy.SameAsRequest
                        : CookieSecurePolicy.Always;
                    options.Cookie.Path = "/";
                    options.SlidingExpiration = true;
                    options.ExpireTimeSpan = TimeSpan.FromMinutes(settings.SessionIdleMinutes);
                    options.AccessDeniedPath = settings.AccessDeniedPath;
                    options.LoginPath = "/bff/login";
                    options.Events.OnRedirectToLogin = context => ApiRedirectAsErrorAsync(
                        context,
                        StatusCodes.Status401Unauthorized,
                        AuthenticationErrorCodes.AuthenticationRequired);
                    options.Events.OnRedirectToAccessDenied = context => ApiRedirectAsErrorAsync(
                        context,
                        StatusCodes.Status403Forbidden,
                        AuthenticationErrorCodes.AuthorizationDenied);
                    options.Events.OnSigningIn = context =>
                    {
                        if (context.Principal is null || !context.HttpContext.RequestServices
                            .GetRequiredService<IValidatedAccountIdentityReader>().TryRead(context.Principal, out _))
                        {
                            throw new InvalidOperationException("A cookie ticket requires one validated account identity.");
                        }
                        return Task.CompletedTask;
                    };
                    options.Events.OnValidatePrincipal = async context =>
                    {
                        if (context.Principal is not null && context.HttpContext.RequestServices
                            .GetRequiredService<IValidatedAccountIdentityReader>().TryRead(context.Principal, out _)) return;
                        context.RejectPrincipal();
                        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
                    };
                });
    }

    /// <summary>Configures the confidential OIDC protocol handler.</summary>
    private static void ConfigureOpenIdConnect(IServiceCollection services)
    {
        services.AddOptions<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme)
            .Configure<IOptions<FoundationWebOptions>, IHostEnvironment, ILoggerFactory>(
                (options, selected, environment, loggerFactory) =>
            {
                var settings = selected.Value;
                var insecureLocal = environment.IsDevelopment() && settings.AllowHttpForLocalDevelopment;
                options.Authority = settings.Authority;
                if (!string.IsNullOrWhiteSpace(settings.BackchannelAuthority))
                {
                    options.MetadataAddress =
                        $"{settings.BackchannelAuthority.TrimEnd('/')}/.well-known/openid-configuration";
                }
                options.ClientId = settings.ClientId;
                options.ClientSecret = settings.ClientSecret;
                options.ResponseType = "code";
                options.ResponseMode = insecureLocal ? "query" : "form_post";
                options.UsePkce = true;
                options.SaveTokens = true;
                options.GetClaimsFromUserInfoEndpoint = true;
                options.MapInboundClaims = false;
                options.RequireHttpsMetadata = !insecureLocal;
                options.CallbackPath = settings.CallbackPath;
                options.SignedOutCallbackPath = settings.SignedOutCallbackPath;
                options.RemoteSignOutPath = settings.RemoteSignOutPath;
                options.RemoteAuthenticationTimeout = TimeSpan.FromSeconds(settings.RemoteAuthenticationTimeoutSeconds);
                options.BackchannelTimeout = TimeSpan.FromSeconds(settings.DiscoveryTimeoutSeconds);
                options.CorrelationCookie.SecurePolicy = insecureLocal
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;
                options.CorrelationCookie.SameSite = insecureLocal
                    ? SameSiteMode.Lax
                    : SameSiteMode.None;
                options.NonceCookie.SecurePolicy = insecureLocal
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;
                options.NonceCookie.SameSite = insecureLocal
                    ? SameSiteMode.Lax
                    : SameSiteMode.None;

                options.Scope.Clear();
                foreach (var scope in settings.Scopes)
                {
                    options.Scope.Add(scope);
                }

                options.TokenValidationParameters = ValidationParameters(settings, settings.ClientId);
                options.Events.OnTokenValidated = context =>
                {
                    if (!ValidatedIdentityTicketProjection.Canonicalize(context))
                    {
                        context.Fail("The validated OpenID Connect identity requires one authenticated identity and one issuer-subject pair.");
                    }
                    return Task.CompletedTask;
                };
                options.Events.OnTicketReceived = context =>
                {
                    if (!ValidatedIdentityTicketProjection.Admit(context, context.HttpContext.RequestServices
                        .GetRequiredService<IValidatedAccountIdentityReader>()))
                    {
                        context.Fail("The validated account projection was not preserved through OpenID Connect claim actions.");
                    }
                    return Task.CompletedTask;
                };
                options.Events.OnRemoteFailure = context =>
                {
                    var code = context.Failure is HttpRequestException or TaskCanceledException
                        ? AuthenticationErrorCodes.IdentityProviderUnavailable
                        : AuthenticationErrorCodes.CallbackInvalid;
                    loggerFactory.CreateLogger("Orbyss.Foundation.RemoteAuthentication")
                        .LogWarning(
                            "OIDC remote authentication failed with stable code {AuthenticationErrorCode} and failure kind {AuthenticationFailureKind}.",
                            code,
                            context.Failure?.GetType().Name ?? "unknown");
                    context.HandleResponse();
                    context.Response.Redirect($"{context.Request.PathBase}{settings.AccessDeniedPath}?code={code}");
                    return Task.CompletedTask;
                };
            });
    }

    /// <summary>Configures the BFF antiforgery token transports and cookie.</summary>
    private static void ConfigureAntiforgery(IServiceCollection services)
    {
        services.AddAntiforgery();
        services.AddOptions<AntiforgeryOptions>()
            .Configure<IOptions<FoundationWebOptions>, IHostEnvironment>((options, selected, environment) =>
            {
                options.HeaderName = AntiforgeryHeader;
                options.FormFieldName = AntiforgeryFormField;
                var insecureLocal = environment.IsDevelopment() && selected.Value.AllowHttpForLocalDevelopment;
                options.Cookie.Name = insecureLocal
                    ? "orbyss-foundation-antiforgery-local"
                    : "__Host-orbyss-foundation-antiforgery";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = insecureLocal
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;
            });
    }

    /// <summary>Clears the local session before attempting provider logout.</summary>
    private static async Task LogoutAsync(HttpContext context, ILoggerFactory loggerFactory)
    {
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
        try
        {
            await context.SignOutAsync(
                OpenIdConnectDefaults.AuthenticationScheme,
                new AuthenticationProperties { RedirectUri = "/bff/signed-out" }).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            loggerFactory.CreateLogger("Orbyss.Foundation.RemoteLogout")
                .LogWarning("Remote logout could not be initiated after the local session was cleared.");
            context.Response.Redirect("/bff/signed-out?remote=unavailable");
        }
    }

    /// <summary>Returns a minimal session projection only for a validated issuer-subject identity.</summary>
    private static async Task<IResult> WriteUserAsync(
        HttpContext context,
        IOptions<FoundationWebOptions> options,
        IValidatedAccountIdentityReader identityReader,
        IJsonResponseFactory<BffUserResponse> responses,
        JsonProfileCatalog profiles)
    {
        var user = context.User;
        if (!user.Identities.Any(identity => identity.IsAuthenticated))
        {
            return responses.Create(new BffAnonymousUserResponse());
        }

        if (!identityReader.TryRead(user, out var account))
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
            return FoundationProblemResults.Problem(new ProblemDefinition(
                StatusCodes.Status401Unauthorized, AuthenticationErrorCodes.IdentityInvalid));
        }

        var permissions = new SortedSet<string>(StringComparer.Ordinal);
        var lowerBoundBytes = 0L;
        var maximumBytes = profiles.Get(new JsonProfileKey(JsonProfileKeys.SuccessResponse)).MaxBytes;
        foreach (var claim in user.FindAll(options.Value.PermissionClaim))
        {
            context.RequestAborted.ThrowIfCancellationRequested();
            if (!permissions.Add(claim.Value)) continue;
            lowerBoundBytes += claim.Value.Length + 3L;
            if (lowerBoundBytes > maximumBytes) throw new JsonOutputLimitException();
        }
        return responses.Create(new BffAuthenticatedUserResponse(account.Issuer, account.Subject,
            user.FindFirstValue("name"), permissions.ToArray()));
    }

    /// <summary>Maps interactive protocol failures to stable authentication error codes.</summary>
    private static Task WriteAccessDeniedAsync(
        HttpContext context,
        string? code,
        IAuthenticationErrorWriter errorWriter)
    {
        var (status, stableCode) = code switch
        {
            AuthenticationErrorCodes.CallbackInvalid =>
                (StatusCodes.Status400BadRequest, AuthenticationErrorCodes.CallbackInvalid),
            AuthenticationErrorCodes.IdentityProviderUnavailable =>
                (StatusCodes.Status503ServiceUnavailable, AuthenticationErrorCodes.IdentityProviderUnavailable),
            _ => (StatusCodes.Status403Forbidden, AuthenticationErrorCodes.AuthorizationDenied)
        };
        return errorWriter.WriteAsync(context, status, stableCode);
    }

    /// <summary>Converts API cookie redirects into an authentication error response.</summary>
    private static Task ApiRedirectAsErrorAsync(
        RedirectContext<CookieAuthenticationOptions> context,
        int status,
        string code)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        }

        return context.HttpContext.RequestServices.GetRequiredService<IAuthenticationErrorWriter>()
            .WriteAsync(context.HttpContext, status, code);
    }

    /// <summary>Creates the issuer, audience, signing-key, and lifetime validation contract.</summary>
    private static TokenValidationParameters ValidationParameters(FoundationWebOptions selected, string audience) =>
        new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name",
            RoleClaimType = selected.RoleClaim
        };

    /// <summary>Returns whether an interactive return target stays inside the application.</summary>
    private static bool IsLocalReturnUrl(string? value) => !string.IsNullOrWhiteSpace(value)
        && value.StartsWith("/", StringComparison.Ordinal)
        && !value.StartsWith("//", StringComparison.Ordinal)
        && !value.StartsWith("/\\", StringComparison.Ordinal);
}
