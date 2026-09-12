using CShells;
using CShells.Features;
using CShells.AspNetCore.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Orbyss.Foundation.WebDefaults;
using System.Security.Cryptography;
using System.Text;

namespace Orbyss.Foundation.Web.HostedPages;

/// <summary>Maps fixed public semantic pages and admitted immutable assets within one shell.</summary>
[ShellFeature(name: "Orbyss.Foundation.Web.HostedPages", DisplayName = "Hosted pages",
    DependsOn = [typeof(FoundationWebDefaultsFeature)])]
public sealed class FoundationHostedPagesFeature(ShellSettings settings) : IWebShellFeature
{
    /// <summary>Names this shell instance's endpoints uniquely for framework URL generation.</summary>
    private readonly string routeNamespace = Guid.NewGuid().ToString("N");
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        services.Configure<HostedPageOptions>(settings.GetConfigurationRoot().GetSection("Foundation:HostedPages"));
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
        MapAsset(endpoints, HostedPageRendering.BuiltInPath(HostedPageRendering.Loader, ".js"), Encoding.UTF8.GetBytes(HostedPageRendering.Loader), "text/javascript; charset=utf-8", "loader.js");
        MapAsset(endpoints, HostedPageRendering.BuiltInPath(HostedPageRendering.Styles, ".css"), Encoding.UTF8.GetBytes(HostedPageRendering.Styles), "text/css; charset=utf-8", "page.css");
        foreach (var revision in catalog.Revisions)
        {
            foreach (var (file, asset) in revision.Files)
                MapAsset(endpoints, HostedPageRendering.AssetPrefix(revision) + file, asset.Bytes, asset.Descriptor.ContentType, Path.GetFileName(file));
            foreach (var locale in revision.Definition.Locales.Keys)
            {
                endpoints.MapMethods(HostedPageRendering.BootstrapPath(revision, locale), ["GET", "HEAD"],
                    (HttpContext context) => Results.Bytes(HostedPageRendering.Bootstrap(revision, locale, route => PublicPath(context, route)), "application/json; charset=utf-8"))
                    .WithName(routeNamespace + HostedPageRendering.BootstrapPath(revision, locale))
                    .WithMetadata(new WebResponseMetadata(Policy: "public-asset", Feature: "Orbyss.Foundation.Web.HostedPages", ImmutablePublicAsset: true, NoIndex: true))
                    .AllowAnonymous().ExcludeFromDescription();
            }
        }
        var current = catalog.Revisions.Single(revision => revision.Definition.Id == catalog.CurrentRevision);
        endpoints.MapMethods(catalog.PagePath, ["GET", "HEAD"], (HttpContext context) =>
        {
            var locale = HostedPageRendering.SelectLocale(context, current.Definition);
            context.Response.Headers.ContentLanguage = locale;
            context.Response.Headers.Vary = "Accept-Language";
            return Results.Content(HostedPageRendering.Page(current, locale, route => PublicPath(context, route)), "text/html; charset=utf-8");
        }).WithMetadata(new WebResponseMetadata(Feature: "Orbyss.Foundation.Web.HostedPages")).AllowAnonymous().ExcludeFromDescription();
    }
    /// <summary>Maps copied immutable bytes with safe names, conditional responses, and explicit public admission.</summary>
    private void MapAsset(IEndpointRouteBuilder endpoints, string route, byte[] data, string contentType, string name)
    {
        var tag = new EntityTagHeaderValue("\"" + Convert.ToHexStringLower(SHA256.HashData(data)) + "\"");
        endpoints.MapMethods(route, ["GET", "HEAD"], (HttpContext context) =>
        {
            context.Response.Headers.ContentDisposition = $"inline; filename=\"{name}\"";
            return Results.Bytes(data, contentType, entityTag: tag, enableRangeProcessing: true);
        }).WithName(routeNamespace + route).WithMetadata(new WebResponseMetadata(Policy: "public-asset", Feature: "Orbyss.Foundation.Web.HostedPages", ImmutablePublicAsset: true, NoIndex: true))
            .AllowAnonymous().ExcludeFromDescription();
    }
    /// <summary>Uses actual mapped route prefixes instead of assuming CShells sets Request.PathBase.</summary>
    private string PublicPath(HttpContext context, string route) =>
        context.RequestServices.GetRequiredService<LinkGenerator>().GetPathByName(context, routeNamespace + route, values: null)
        ?? throw new InvalidOperationException("Hosted public route is unavailable.");
}
