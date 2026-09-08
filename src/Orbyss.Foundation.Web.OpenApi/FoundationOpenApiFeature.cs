using CShells.AspNetCore.Features;
using CShells.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Orbyss.Foundation.Web.OpenApi;

/// <summary>Registers and maps the optional shell-owned Orbyss Foundation OpenAPI document.</summary>
[ShellFeature(
    name: "Orbyss.Foundation.Web.OpenApi",
    DisplayName = "Orbyss Foundation OpenAPI",
    Description = "Provides the default shell-owned OpenAPI document endpoint.")]
public sealed class FoundationOpenApiFeature : IWebShellFeature
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services) => services.AddOpenApi("v1");

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment) =>
        endpoints.MapOpenApi("/_orbyss-foundation/openapi/{documentName}.json");
}
