using CShells;
using CShells.Features;
using CShells.AspNetCore.Features;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Orbyss.Foundation.Authentication;
using Orbyss.Foundation.WebDefaults;
using System.Security.Cryptography;
using System.Text;

namespace Orbyss.Foundation.Web.HostedPages;

/// <summary>Maps admitted public or session-protected application pages and explicitly public assets in one shell.</summary>
[ShellFeature(name: "Orbyss.Foundation.Web.HostedPages", DisplayName = "Hosted pages",
    DependsOn = [typeof(FoundationWebDefaultsFeature)])]
public sealed class FoundationHostedPagesFeature(ShellSettings settings) : IWebShellFeature
{
    /// <summary>Names the independently selected feature and its response-policy owner.</summary>
    public const string FeatureName = "Orbyss.Foundation.Web.HostedPages";
    /// <summary>Names this shell generation's routes uniquely for URL generation.</summary>
    private readonly string routeNamespace = Guid.NewGuid().ToString("N");
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        var configuration = settings.GetConfigurationRoot().GetSection(HostedPageOptions.SectionName);
        services.Configure<HostedPageOptions>(options =>
        {
            var routes = configuration.GetChildren().FirstOrDefault(section => section.Key.Equals(nameof(HostedPageOptions.Routes), StringComparison.OrdinalIgnoreCase));
            if (routes is not null)
            {
                var entries = routes.GetChildren().ToArray();
                if (routes.Value is not null || entries.Length == 0 || entries.Where((entry, index) =>
                    entry.Key != index.ToString(System.Globalization.CultureInfo.InvariantCulture) || entry.Value is not null || !entry.GetChildren().Any()).Any())
                    throw new InvalidDataException("Explicit Routes must be a non-empty indexed collection.");
                if (configuration.GetChildren().Any(section => section.Key.Equals(nameof(HostedPageOptions.PagePath), StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException("PagePath cannot be combined with explicit Routes.");
            }
            configuration.Bind(options, binder => binder.ErrorOnUnknownConfiguration = true);
        });
        services.TryAddSingleton<IHostedAssetSource>(provider => new LocalHostedAssetSource(
            provider.GetRequiredService<IHostEnvironment>().ContentRootPath,
            provider.GetRequiredService<IOptions<HostedPageOptions>>().Value.Root));
        services.AddSingleton(provider => new HostedPageCatalog(provider.GetRequiredService<IOptions<HostedPageOptions>>().Value,
            provider.GetRequiredService<IHostedAssetSource>()));
    }
    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment)
    {
        var catalog = endpoints.ServiceProvider.GetRequiredService<HostedPageCatalog>();
        var policies = endpoints.ServiceProvider.GetRequiredService<WebResponsePolicyCatalog>();
        ValidateSelection(endpoints.ServiceProvider, policies, new(false, null, catalog.AssetResponsePolicy));
        foreach (var route in catalog.Routes)
        {
            ValidateSelection(endpoints.ServiceProvider, policies, route.Page);
            ValidateSelection(endpoints.ServiceProvider, policies, route.Bootstrap);
        }
        MapAsset(endpoints, HostedPageRendering.BuiltInPath(HostedPageRendering.Loader, ".js"), Encoding.UTF8.GetBytes(HostedPageRendering.Loader), "text/javascript; charset=utf-8", "loader.js", catalog.AssetResponsePolicy);
        MapAsset(endpoints, HostedPageRendering.BuiltInPath(HostedPageRendering.Styles, ".css"), Encoding.UTF8.GetBytes(HostedPageRendering.Styles), "text/css; charset=utf-8", "page.css", catalog.AssetResponsePolicy);
        foreach (var revision in catalog.Revisions)
            foreach (var (file, asset) in revision.Files)
                MapAsset(endpoints, HostedPageRendering.AssetPrefix(revision) + file, asset.Bytes, asset.Descriptor.ContentType, Path.GetFileName(file), catalog.AssetResponsePolicy);
        foreach (var route in catalog.Routes)
        {
            // Explicit routes admit only their selected revision. Legacy mode retains public hash URLs.
            var revisions = route.Id is null ? catalog.Revisions : catalog.Revisions.Where(revision => revision.Definition.Id == route.Revision);
            foreach (var revision in revisions)
                foreach (var locale in revision.Definition.Locales.Keys)
                {
                    var path = HostedPageRendering.BootstrapPath(revision, locale, route.Id);
                    var bootstrap = endpoints.MapMethods(path, ["GET", "HEAD"],
                        (HttpContext context) => Results.Bytes(HostedPageRendering.Bootstrap(revision, locale, target => MappedPath(context, target)), "application/json; charset=utf-8"))
                        .WithName(routeNamespace + path)
                        .WithMetadata(Metadata(route.Bootstrap, immutable: !route.Bootstrap.Private, noIndex: true))
                        .ExcludeFromDescription();
                    ApplyAccess(bootstrap, route.Page, route.Bootstrap);
                }
            var current = catalog.Revisions.Single(revision => revision.Definition.Id == route.Revision);
            var page = endpoints.MapMethods(route.Path, ["GET", "HEAD"], (HttpContext context) =>
            {
                var locale = HostedPageRendering.SelectLocale(context, current.Definition);
                context.Response.Headers.ContentLanguage = locale;
                context.Response.Headers.Vary = "Accept-Language";
                return Results.Content(HostedPageRendering.Page(current, locale, target => MappedPath(context, target), route), "text/html; charset=utf-8");
            }).WithMetadata(Metadata(route.Page)).ExcludeFromDescription();
            ApplyAccess(page, route.Page);
        }
    }
    /// <summary>Rejects absent session composition and unknown selectors before mapping even assets.</summary>
    private static void ValidateSelection(IServiceProvider services, WebResponsePolicyCatalog policies, HostedPageAdmission access)
    {
        _ = policies.Resolve(new Endpoint(null, new EndpointMetadataCollection(Metadata(access)), null));
        if (!access.Private) return;
        if (services.GetServices<IFoundationAuthenticationProfile>().SingleOrDefault()?.Name != FoundationAuthenticationProfiles.BffCookie ||
            services.GetService<IAuthenticationSchemeProvider>()?.GetSchemeAsync(CookieAuthenticationDefaults.AuthenticationScheme).GetAwaiter().GetResult() is null)
            throw new InvalidDataException("Authenticated hosted pages require the independently selected Foundation BFF-cookie profile.");
        if (access.Permission is not null && services.GetRequiredService<IAuthorizationPolicyProvider>()
            .GetPolicyAsync(PermissionPolicyNames.ForPermission(access.Permission)).GetAwaiter().GetResult() is null)
            throw new InvalidDataException("Hosted route permission policy is unavailable.");
    }
    /// <summary>Adds either anonymous admission or session and conjunctive canonical permissions.</summary>
    private static void ApplyAccess(RouteHandlerBuilder endpoint, params HostedPageAdmission[] selections)
    {
        if (!selections.Any(access => access.Private)) { endpoint.AllowAnonymous(); return; }
        endpoint.RequireAuthorization(new AuthorizationPolicyBuilder(CookieAuthenticationDefaults.AuthenticationScheme).RequireAuthenticatedUser().Build());
        foreach (var permission in selections.Select(access => access.Permission).OfType<string>().Distinct(StringComparer.Ordinal))
            endpoint.RequireAuthorization(PermissionPolicyNames.ForPermission(permission));
    }
    /// <summary>Selects the existing final header writer without emitting headers here.</summary>
    private static WebResponseMetadata Metadata(HostedPageAdmission access, bool immutable = false, bool noIndex = false) =>
        new(Policy: access.Policy, Feature: FeatureName, Private: access.Private, ImmutablePublicAsset: immutable, NoIndex: noIndex);
    /// <summary>Maps immutable bytes with explicit public admission, safe names and conditional responses.</summary>
    private void MapAsset(IEndpointRouteBuilder endpoints, string route, byte[] data, string contentType, string name, string policy)
    {
        var tag = new EntityTagHeaderValue("\"" + Convert.ToHexStringLower(SHA256.HashData(data)) + "\"");
        endpoints.MapMethods(route, ["GET", "HEAD"], (HttpContext context) =>
        {
            context.Response.Headers.ContentDisposition = $"inline; filename=\"{name}\"";
            return Results.Bytes(data, contentType, entityTag: tag, enableRangeProcessing: true);
        }).WithName(routeNamespace + route).WithMetadata(Metadata(new(false, null, policy), immutable: true, noIndex: true))
            .AllowAnonymous().ExcludeFromDescription();
    }
    /// <summary>Uses the actual mapped shell prefix, including after shell recreation.</summary>
    private string MappedPath(HttpContext context, string route) =>
        context.RequestServices.GetRequiredService<LinkGenerator>().GetPathByName(context, routeNamespace + route, values: null)
        ?? throw new InvalidOperationException("Hosted route is unavailable.");
}
