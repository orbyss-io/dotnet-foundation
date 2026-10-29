using System.Collections.Frozen;
using System.Text;
using CShells;
using CShells.AspNetCore.Features;
using CShells.Features;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orbyss.Foundation.Authentication;
using Orbyss.Foundation.Web.ProblemDetails;
using Orbyss.Foundation.Web.ProblemDetails.Core;
using Orbyss.Foundation.WebDefaults;

namespace Orbyss.Foundation.Web.DynamicPages;

/// <summary>Maps opt-in live application pages while retaining native authentication, permission and final-header ownership.</summary>
[ShellFeature(name: "Orbyss.Foundation.Web.DynamicPages", DisplayName = "Dynamic pages",
    DependsOn = [typeof(FoundationWebDefaultsFeature), typeof(FoundationProblemDetailsFeature)])]
public sealed class FoundationDynamicPagesFeature(ShellSettings settings) : IWebShellFeature
{
    /// <summary>Names the separately selected feature.</summary>
    public const string FeatureName = "Orbyss.Foundation.Web.DynamicPages";
    /// <summary>Names a public data-reading utility used to derive native shell URLs.</summary>
    private const string RuntimePath = "/_foundation/dynamic-pages/v1/page.mjs";
    /// <summary>Isolates URL generation across shell generations.</summary>
    private readonly string routeNamespace = Guid.NewGuid().ToString("N");
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        var configuration = settings.GetConfigurationRoot().GetSection(DynamicPageOptions.SectionName);
        services.Configure<DynamicPageOptions>(options =>
        {
            var routes = configuration.GetSection(nameof(DynamicPageOptions.Routes));
            var entries = routes.GetChildren().ToArray();
            if (routes.Value is not null || entries.Length == 0 || entries.Where((entry, index) =>
                entry.Key != index.ToString(System.Globalization.CultureInfo.InvariantCulture) || entry.Value is not null || !entry.GetChildren().Any()).Any())
                throw new InvalidDataException("Dynamic page Routes must be a non-empty indexed collection.");
            configuration.Bind(options, binder => binder.ErrorOnUnknownConfiguration = true);
        });
        services.AddSingleton(provider => new DynamicPageCatalog(provider.GetRequiredService<IOptions<DynamicPageOptions>>().Value,
            provider.GetServices<DynamicPageResolverRegistration>()));
        services.TryAddSingleton<IDynamicPageRenderer, DefaultDynamicPageRenderer>();
    }
    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment)
    {
        var catalog = endpoints.ServiceProvider.GetRequiredService<DynamicPageCatalog>();
        var policies = endpoints.ServiceProvider.GetRequiredService<WebResponsePolicyCatalog>();
        foreach (var route in catalog.Routes)
        {
            var metadata = Metadata(route);
            _ = policies.Resolve(new Endpoint(null, new EndpointMetadataCollection(metadata), null));
            if (route.Mode != "Authenticated") continue;
            if (endpoints.ServiceProvider.GetServices<IFoundationAuthenticationProfile>().SingleOrDefault()?.Name != FoundationAuthenticationProfiles.BffCookie
                || endpoints.ServiceProvider.GetService<IAuthenticationSchemeProvider>()?.GetSchemeAsync(CookieAuthenticationDefaults.AuthenticationScheme).GetAwaiter().GetResult() is null)
                throw new InvalidDataException("Authenticated dynamic pages require the independently selected native BFF-cookie profile.");
            if (route.Permission is not null && endpoints.ServiceProvider.GetRequiredService<IAuthorizationPolicyProvider>()
                .GetPolicyAsync(PermissionPolicyNames.ForPermission(route.Permission)).GetAwaiter().GetResult() is null)
                throw new InvalidDataException("Dynamic page permission policy is unavailable.");
        }
        endpoints.MapMethods(RuntimePath, ["GET", "HEAD"], () => Results.Text(
            "export function readPageData(){const node=document.getElementById('foundation-page-data');if(!node||node.type!=='application/json')throw new Error('Page data unavailable');return JSON.parse(node.textContent);}",
            "text/javascript; charset=utf-8"))
            .WithName(routeNamespace + RuntimePath).WithMetadata(new WebResponseMetadata(NoIndex: true),
                new ProblemResponsePurposeMetadata(ProblemResponsePurpose.Machine)).AllowAnonymous().ExcludeFromDescription();
        foreach (var route in catalog.Routes)
        {
            var page = endpoints.MapMethods(route.Path, ["GET", "HEAD"], (HttpContext context) => WriteAsync(context, catalog, route))
                .WithMetadata(Metadata(route), new ProblemResponsePurposeMetadata(ProblemResponsePurpose.BrowserPage))
                .CacheOutput(policy => policy.NoCache()).ExcludeFromDescription();
            if (route.Mode == "Public") page.AllowAnonymous();
            else
            {
                page.RequireAuthorization(new AuthorizationPolicyBuilder(CookieAuthenticationDefaults.AuthenticationScheme).RequireAuthenticatedUser().Build());
                if (route.Permission is not null) page.RequireAuthorization(PermissionPolicyNames.ForPermission(route.Permission));
            }
        }
    }
    /// <summary>Resolves and renders before a second current-resource admission, committing no resource bytes on failure.</summary>
    private async Task WriteAsync(HttpContext context, DynamicPageCatalog catalog, DynamicPageRouteOptions route)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        deadline.CancelAfter(TimeSpan.FromSeconds(catalog.TimeoutSeconds));
        var token = deadline.Token;
        byte[] bytes;
        string locale;
        DynamicPageStatus status;
        try
        {
            var mappedRuntime = context.RequestServices.GetRequiredService<LinkGenerator>().GetPathByName(context, routeNamespace + RuntimePath, null)
                ?? throw new InvalidOperationException("Dynamic page runtime route is unavailable.");
            var pathBase = mappedRuntime[..^RuntimePath.Length];
            if (pathBase.Length > 1024 || pathBase.Length > 0 && !DynamicPageCatalog.LocalPath(pathBase))
                throw new InvalidDataException("Dynamic page application prefix is invalid.");
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in context.Request.RouteValues)
            {
                var text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "";
                if (text.Length > 256 || text.Any(char.IsControl)) throw new InvalidDataException("Dynamic page route value is invalid.");
                values.Add(key, text);
            }
            var culture = context.Features.Get<Microsoft.AspNetCore.Localization.IRequestCultureFeature>()?.RequestCulture.UICulture.Name;
            var request = new DynamicPageRequest(context.User, values.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase), pathBase)
                { Locale = string.IsNullOrEmpty(culture) ? "en" : culture };
            var resolver = (IDynamicPageResolver)context.RequestServices.GetRequiredService(catalog.Resolvers[route.Resolver]);
            var resolution = await resolver.ResolveAsync(request, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (resolution.Status != DynamicPageStatus.Admitted)
            {
                await RejectAsync(context, resolution.Status).ConfigureAwait(false);
                return;
            }
            if (resolution.Version is not { Length: > 0 and <= 256 } || resolution.Version.Any(char.IsControl) || resolution.Document is null)
                throw new InvalidDataException("Admitted dynamic pages require a coherent version and document.");
            var admitted = catalog.Admit(resolution.Document, route, pathBase, token);
            var html = context.RequestServices.GetRequiredService<IDynamicPageRenderer>().Render(admitted);
            var encoding = new UTF8Encoding(false, true);
            if (encoding.GetByteCount(html) > catalog.MaxResponseBytes) throw new InvalidDataException("Dynamic page output exceeds its byte budget.");
            bytes = encoding.GetBytes(html);
            locale = admitted.Locale;
            token.ThrowIfCancellationRequested();
            status = await resolver.VerifyAsync(request, resolution.Version, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
        }
        catch (Exception error) when (!context.RequestAborted.IsCancellationRequested)
        {
            context.RequestServices.GetRequiredService<ILogger<FoundationDynamicPagesFeature>>()
                .LogWarning("Dynamic page rejected {FailureType} for route {RouteId}.", error.GetType().Name, route.Id);
            await RejectAsync(context, DynamicPageStatus.Unavailable).ConfigureAwait(false);
            return;
        }
        context.RequestAborted.ThrowIfCancellationRequested();
        if (status != DynamicPageStatus.Admitted)
        {
            await RejectAsync(context, status).ConfigureAwait(false);
            return;
        }
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength = bytes.Length;
        context.Response.Headers.ContentLanguage = locale;
        context.Response.Headers.Vary = "Accept-Language";
        context.Response.Headers.CacheControl = "no-store";
        if (!HttpMethods.IsHead(context.Request.Method)) await context.Response.Body.WriteAsync(bytes, context.RequestAborted).ConfigureAwait(false);
    }
    /// <summary>Keeps response classification separate from admission and prevents immutable/public cache classification.</summary>
    private static WebResponseMetadata Metadata(DynamicPageRouteOptions route) =>
        new(Policy: route.ResponsePolicy, Feature: FeatureName, Private: route.Mode == "Authenticated", NoIndex: route.NoIndex);
    /// <summary>Returns a stable bounded failure without disclosing the resource projection or arbitrary provider text.</summary>
    private static Task RejectAsync(HttpContext context, DynamicPageStatus status)
    {
        var (http, code) = status switch
        {
            DynamicPageStatus.NotFound => (404, "resource_not_found"),
            DynamicPageStatus.Forbidden => (403, "authorization_denied"),
            _ => (503, "dynamic_page_unavailable")
        };
        return FoundationProblemResults.Problem(new ProblemDefinition(http, code)).ExecuteAsync(context);
    }
}
