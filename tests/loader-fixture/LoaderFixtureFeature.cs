using System.Globalization;
using System.Resources;
using System.Runtime.Loader;
using System.Security.Cryptography;
using CShells.AspNetCore.Features;
using CShells.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UglyToad.PdfPig;

namespace LoaderFixture;

/// <summary>Only synthetic package-loader observations; no domain/provider behavior.</summary>
[ShellFeature(name: "Foundation.LoaderFixture")]
public sealed class LoaderFixtureFeature : IWebShellFeature
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services) { }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints, IHostEnvironment? environment)
    {
        endpoints.MapGet("/loader-proof", () =>
        {
            var assembly = typeof(LoaderFixtureFeature).Assembly;
            var context = AssemblyLoadContext.GetLoadContext(assembly)!;
            var copy = new ResourceManager("LoaderFixture.Copy", assembly);
            var english = copy.GetString("Title", CultureInfo.GetCultureInfo("en"));
            var dutch = copy.GetString("Title", CultureInfo.GetCultureInfo("nl"));
            var fallback = copy.GetString("Title", CultureInfo.GetCultureInfo("nl-BE"));
            using var document = PdfDocument.Open(File.ReadAllBytes(Path.Combine(environment!.ContentRootPath, "known.pdf")));
            var text = document.GetPage(1).Text;
            var loaded = context.Assemblies.Where(value => value.GetName().Name!.StartsWith("UglyToad.PdfPig", StringComparison.Ordinal)
                || value.GetName().Name == "LoaderFixture.resources").Select(value => new
                {
                    name = value.GetName().Name, culture = value.GetName().CultureName ?? "",
                    sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(value.Location)))
                }).OrderBy(value => value.name).ThenBy(value => value.culture).ToArray();
            return new { english, dutch, fallback, pages = document.NumberOfPages, text,
                hostIntegratedContext = context == AssemblyLoadContext.Default, loaded };
        });
    }
}
