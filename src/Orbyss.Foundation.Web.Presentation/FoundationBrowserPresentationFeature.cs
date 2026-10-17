using CShells;
using CShells.AspNetCore.Features;
using CShells.Features;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Orbyss.Foundation.Authentication;
using Orbyss.Foundation.Web.ProblemDetails;
using Orbyss.Foundation.Web.ProblemDetails.Core;
using Orbyss.Foundation.WebDefaults;
using Orbyss.Foundation.Json;
using Orbyss.Foundation.Json.AspNetCore;

namespace Orbyss.Foundation.Web.Presentation;

/// <summary>Maps independently selected public browser recovery and native authentication controls.</summary>
[ShellFeature(name: "Orbyss.Foundation.Web.Presentation", DisplayName = "Browser presentation",
    DependsOn = [typeof(FoundationWebDefaultsFeature), typeof(FoundationProblemDetailsFeature), typeof(FoundationJsonFeature)])]
public sealed class FoundationBrowserPresentationFeature(ShellSettings settings) : IWebShellFeature
{
    /// <summary>Separates URL generation across live shell generations.</summary>
    private readonly string routeNamespace = Guid.NewGuid().ToString("N");
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        var configuration = settings.GetConfigurationRoot().GetSection(BrowserPresentationOptions.SectionName);
        services.Configure<BrowserPresentationOptions>(options => configuration.Bind(options, binder => binder.ErrorOnUnknownConfiguration = true));
        services.AddSingleton(provider => new BrowserPresentationCatalog(provider.GetRequiredService<IOptions<BrowserPresentationOptions>>().Value));
        services.TryAddSingleton<IProblemPageRenderer, BrowserProblemPageRenderer>();
        services.AddJsonResponseContract<BrowserPresentationBootstrap>(new JsonProfileKey(JsonProfileKeys.SuccessResponse),
            new JsonProfileRequirement(JsonProfileKeys.TolerantResponse));
    }
    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment)
    {
        var catalog = endpoints.ServiceProvider.GetRequiredService<BrowserPresentationCatalog>();
        var session = endpoints.ServiceProvider.GetService<IBrowserAuthenticationSession>();
        var policy = endpoints.ServiceProvider.GetRequiredService<WebResponsePolicyCatalog>();
        foreach (var name in new[] { catalog.ResponsePolicy, catalog.AssetResponsePolicy })
            _ = policy.Resolve(new Endpoint(null, new EndpointMetadataCollection(new WebResponseMetadata(Policy: name)), null));
        if (session is not null)
        {
            var native = endpoints.ServiceProvider.GetRequiredService<IOptions<FoundationWebOptions>>().Value;
            if (native.LogoutLandingPath != catalog.RoutePrefix + "/signed-out")
                throw new InvalidDataException("Native LogoutLandingPath must select the configured browser signed-out landing.");
            foreach (var path in new[] { native.CallbackPath, native.SignedOutCallbackPath, native.RemoteSignOutPath, native.AccessDeniedPath })
                if (path.StartsWith(catalog.RoutePrefix + "/", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Presentation routes collide with a native authentication route.");
        }
        endpoints.MapGet(catalog.RoutePrefix + "/configuration", (HttpContext context, IJsonResponseFactory<BrowserPresentationBootstrap> responses) =>
        {
            var pathBase = PathBase(context, catalog);
            return responses.Create(new("foundation-browser-presentation/v1", pathBase.Length == 0 ? "/" : pathBase,
                catalog.SignOutConfirmation, pathBase + catalog.RoutePrefix + "/assets/v1/presentation.mjs",
                session is null ? null : pathBase + catalog.RoutePrefix + "/assets/v1/bff.mjs",
                session is null ? null : pathBase + "/bff/antiforgery", session is null ? null : pathBase + "/bff/logout"));
        }).WithJsonResponse<BrowserPresentationBootstrap>().WithMetadata(PageMetadata(catalog),
            new ProblemResponsePurposeMetadata(ProblemResponsePurpose.Machine)).AllowAnonymous().ExcludeFromDescription();
        foreach (var kind in new[] { "sign-in", "sign-out", "signed-out", "expired", "denied" })
        {
            var route = catalog.RoutePrefix + "/" + kind;
            endpoints.MapMethods(route, ["GET", "HEAD"], (HttpContext context) =>
            {
                var pathBase = PathBase(context, catalog);
                var active = session?.IsAuthenticated(context);
                var content = "<p>" + BrowserPageRendering.E(catalog.Copy[active switch { true => "active", false => "anonymous", _ => "unobserved" }]) + "</p>";
                if (kind == "signed-out" && context.Request.Query["remote"] == "unavailable")
                    content += "<p role=\"status\">" + BrowserPageRendering.E(catalog.Copy["remote"]) + "</p>";
                if (session is null)
                    content += "<p>" + BrowserPageRendering.E(catalog.Copy["client-authentication"]) + "</p>";
                else if (kind == "sign-out")
                {
                    var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
                    var tokens = antiforgery.GetAndStoreTokens(context);
                    content += "<p>" + BrowserPageRendering.E(catalog.Copy["confirm"]) + "</p><form method=\"post\" action=\""
                        + BrowserPageRendering.E(MappedPath(context, route)) + "\"><input type=\"hidden\" name=\""
                        + BrowserPageRendering.E(tokens.FormFieldName) + "\" value=\"" + BrowserPageRendering.E(tokens.RequestToken!)
                        + "\"><button type=\"submit\">" + BrowserPageRendering.E(catalog.Copy["sign-out"]) + "</button></form>";
                }
                else if (kind != "denied" || active != true)
                {
                    var tokens = context.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(context);
                    content += "<form method=\"post\" action=\"" + BrowserPageRendering.E(MappedPath(context, catalog.RoutePrefix + "/sign-in"))
                        + "\"><input type=\"hidden\" name=\"" + BrowserPageRendering.E(tokens.FormFieldName)
                        + "\" value=\"" + BrowserPageRendering.E(tokens.RequestToken!)
                        + "\"><button type=\"submit\">" + BrowserPageRendering.E(catalog.Copy["sign-in"]) + "</button></form>";
                }
                context.Response.Headers.ContentLanguage = catalog.Locale;
                return Results.Content(BrowserPageRendering.Page(catalog, catalog.Copy[kind], content, pathBase), "text/html; charset=utf-8");
            }).WithName(routeNamespace + route).WithMetadata(PageMetadata(catalog),
                new ProblemResponsePurposeMetadata(ProblemResponsePurpose.BrowserPage)).AllowAnonymous().ExcludeFromDescription();
        }
        foreach (var action in session is null ? Array.Empty<string>() : new[] { "sign-in", "sign-out" })
            endpoints.MapPost(catalog.RoutePrefix + "/" + action, async (HttpContext context, IAntiforgery antiforgery) =>
            {
                try { await antiforgery.ValidateRequestAsync(context).ConfigureAwait(false); }
                catch (AntiforgeryValidationException)
                {
                    await context.RequestServices.GetRequiredService<IAuthenticationErrorWriter>()
                        .WriteAsync(context, 400, "antiforgery_invalid").ConfigureAwait(false);
                    return;
                }
                if (action == "sign-in") await session!.SignInAsync(context, PathBase(context, catalog) + catalog.ReturnPath).ConfigureAwait(false);
                else await session!.SignOutAsync(context).ConfigureAwait(false);
            }).WithMetadata(PageMetadata(catalog), new ProblemResponsePurposeMetadata(ProblemResponsePurpose.BrowserPage))
                .AllowAnonymous().ExcludeFromDescription();
        foreach (var file in new[] { "configuration.mjs", "configuration.d.mts", "presentation.mjs", "presentation.d.mts", "bff.mjs", "bff.d.mts", "spa.mjs", "spa.d.mts", "session.mjs", "session.d.mts", "page.css" })
        {
            using var resource = typeof(FoundationBrowserPresentationFeature).Assembly.GetManifestResourceStream(
                "Orbyss.Foundation.Web.Presentation.browser." + file) ?? throw new InvalidDataException("Presentation asset is missing.");
            using var memory = new MemoryStream();
            resource.CopyTo(memory);
            var bytes = memory.ToArray();
            endpoints.MapMethods(catalog.RoutePrefix + "/assets/v1/" + file, ["GET", "HEAD"], () => Results.Bytes(bytes,
                file.EndsWith(".css", StringComparison.Ordinal) ? "text/css; charset=utf-8" : "text/javascript; charset=utf-8"))
                .WithName(routeNamespace + catalog.RoutePrefix + "/assets/v1/" + file)
                .WithMetadata(new WebResponseMetadata(Policy: catalog.AssetResponsePolicy, NoIndex: true) { NoFollow = true },
                    new ProblemResponsePurposeMetadata(ProblemResponsePurpose.Machine)).AllowAnonymous().ExcludeFromDescription();
        }
        endpoints.MapFallback("/{**path}", (HttpContext context) =>
        {
            if (BrowserNavigationAdmission.IsDocumentNavigation(context.Request))
                context.Features.Set(new ProblemResponsePurposeMetadata(ProblemResponsePurpose.BrowserPage));
            return FoundationProblemResults.Problem(new ProblemDefinition(404, "resource_not_found"));
        }).WithMetadata(PageMetadata(catalog)).AllowAnonymous().ExcludeFromDescription();
    }
    /// <summary>Uses native route generation after CShells restores the outer request path.</summary>
    private string MappedPath(HttpContext context, string route) => context.RequestServices.GetRequiredService<LinkGenerator>()
        .GetPathByName(context, routeNamespace + route, null) ?? throw new InvalidOperationException("Presentation route is unavailable.");
    /// <summary>Derives the native prefix from a mapped route without trusting headers or a query target.</summary>
    private string PathBase(HttpContext context, BrowserPresentationCatalog catalog) =>
        MappedPath(context, catalog.RoutePrefix + "/sign-in")[..^(catalog.RoutePrefix.Length + "/sign-in".Length)];
    /// <summary>Selects private response headers for every recovery page and protocol action.</summary>
    private static WebResponseMetadata PageMetadata(BrowserPresentationCatalog catalog) =>
        new(Policy: catalog.ResponsePolicy, Private: true, NoIndex: true) { NoFollow = true };
}
