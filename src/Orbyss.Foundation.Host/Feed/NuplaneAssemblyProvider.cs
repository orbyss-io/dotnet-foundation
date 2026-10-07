using System.Reflection;
using System.Runtime.Loader;
using CShells.Features;
using Nuplane.Loading;

namespace Orbyss.Foundation.Host.Feed;

/// <summary>Supplies feature assemblies from Nuplane's immutable package catalog.</summary>
internal sealed class NuplaneAssemblyProvider(IPackageAssemblyCatalog packageAssemblyCatalog) : IFeatureAssemblyProvider, IDisposable
{
    /// <summary>Maps exact native identities to their installed immutable assembly paths.</summary>
    private IReadOnlyDictionary<string, string> assemblySnapshot = new Dictionary<string, string>();
    /// <summary>Records whether this Host owns an active default-context resolver.</summary>
    private bool connected;
    /// <summary>Serializes publication of the snapshot and host-context loading.</summary>
    private readonly object synchronization = new();

    /// <inheritdoc />
    public async Task<IEnumerable<Assembly>> GetAssembliesAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        var packages = await packageAssemblyCatalog.GetAssembliesAsync(cancellationToken).ConfigureAwait(false);
        var assemblies = packages.SelectMany(package => package.Assemblies).Distinct().ToArray();
        // Foundation's typed cross-package contracts require a common lifetime.
        // Nuplane 1.0 removed HostIntegrated mode; keep that policy at the Host
        // boundary using the exact installed paths owned by its native catalog.
        var identities = assemblies.Where(assembly => assembly.FullName is not null)
            .GroupBy(assembly => assembly.FullName!, StringComparer.OrdinalIgnoreCase).ToArray();
        if (identities.Any(group => group.Count() != 1))
            throw new InvalidOperationException("The native package catalog contains ambiguous assembly identities.");

        lock (synchronization)
        {
            assemblySnapshot = identities.ToDictionary(group => group.Key, group => group.Single().Location, StringComparer.OrdinalIgnoreCase);
            if (!connected)
            {
                AssemblyLoadContext.Default.Resolving += ResolveCatalogAssembly;
                connected = true;
            }
            return identities.Select(group => LoadHostAssembly(group.Key, group.Single().Location)).ToArray();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (synchronization)
        {
            if (connected)
                AssemblyLoadContext.Default.Resolving -= ResolveCatalogAssembly;
            connected = false;
            assemblySnapshot = new Dictionary<string, string>();
        }
    }

    /// <summary>Resolves an exact cross-package reference from the native installed paths.</summary>
    private Assembly? ResolveCatalogAssembly(AssemblyLoadContext context, AssemblyName requested)
    {
        lock (synchronization)
            return requested.FullName is { } identity && assemblySnapshot.TryGetValue(identity, out var path)
                ? LoadHostAssembly(identity, path) : null;
    }

    /// <summary>Loads the selected identity into the Host lifetime and rejects stale replacements.</summary>
    private static Assembly LoadHostAssembly(string identity, string path)
    {
        if (!Path.IsPathFullyQualified(path) || !File.Exists(path)
            || !string.Equals(AssemblyName.GetAssemblyName(path).FullName, identity, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The installed native assembly differs from its catalog identity.");
        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
        if (!string.Equals(assembly.FullName, identity, StringComparison.OrdinalIgnoreCase))
        {
            var requested = new AssemblyName(identity);
            var loaded = assembly.GetName();
            // The selected shared framework supplies its platform assemblies;
            // signed package servicing can also satisfy an older same-major reference.
            var framework = requested.Name is { } name
                && (name.StartsWith("Microsoft.", StringComparison.Ordinal) || name.StartsWith("System.", StringComparison.Ordinal))
                && requested.GetPublicKeyToken() is { Length: > 0 } token
                && token.SequenceEqual(loaded.GetPublicKeyToken() ?? [])
                && string.Equals(requested.Name, loaded.Name, StringComparison.OrdinalIgnoreCase)
                && string.Equals(requested.CultureName, loaded.CultureName, StringComparison.OrdinalIgnoreCase)
                && requested.Version is { } version && loaded.Version is { } actual
                && actual >= version
                && (actual.Major == version.Major
                    || string.Equals(Path.GetDirectoryName(assembly.Location), Path.GetDirectoryName(typeof(object).Assembly.Location), StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Path.GetDirectoryName(assembly.Location), Path.GetDirectoryName(typeof(IResult).Assembly.Location), StringComparison.OrdinalIgnoreCase));
            if (!framework)
                throw new InvalidOperationException($"Package identity '{identity}' differs from loaded '{assembly.FullName}'; restart the Host before activating it.");
        }
        return assembly;
    }

}
