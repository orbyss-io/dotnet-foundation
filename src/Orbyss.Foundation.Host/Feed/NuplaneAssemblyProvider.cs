using System.Reflection;
using CShells.Features;
using Nuplane.Loading;

namespace Orbyss.Foundation.Host.Feed;

/// <summary>Supplies feature assemblies from Nuplane's immutable package catalog.</summary>
internal sealed class NuplaneAssemblyProvider(IPackageAssemblyCatalog packageAssemblyCatalog) : IFeatureAssemblyProvider
{
    /// <inheritdoc />
    public async Task<IEnumerable<Assembly>> GetAssembliesAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default) =>
        await packageAssemblyCatalog.GetAssembliesAsync(cancellationToken).ConfigureAwait(false);
}
