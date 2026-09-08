using System.Reflection;
using System.Runtime.Loader;

namespace Orbyss.Foundation.OpenApiExport;

/// <summary>Resolves dependencies only from the validated staged package closure.</summary>
internal sealed class AssemblyResolver : IDisposable
{
    /// <summary>Maps simple assembly names to hash-covered bytes from staged packages.</summary>
    private readonly IReadOnlyDictionary<string, byte[]> _assemblies;

    /// <summary>Registers a temporary resolver for staged package assemblies.</summary>
    public AssemblyResolver(IReadOnlyDictionary<string, byte[]> assemblies)
    {
        _assemblies = assemblies;
        AssemblyLoadContext.Default.Resolving += Resolve;
    }

    /// <summary>Loads a feature assembly into the exporter process.</summary>
    public Assembly Load(string name) => LoadBytes(_assemblies[name]);

    /// <summary>Unregisters the temporary staged-package resolver.</summary>
    public void Dispose() => AssemblyLoadContext.Default.Resolving -= Resolve;

    /// <summary>Resolves a requested dependency from the extracted package closure.</summary>
    private Assembly? Resolve(AssemblyLoadContext context, AssemblyName name) =>
        name.Name is not null && _assemblies.TryGetValue(name.Name, out var content)
            ? LoadBytes(content)
            : null;

    /// <summary>Loads immutable package bytes without creating a locked extraction file.</summary>
    private static Assembly LoadBytes(byte[] content)
    {
        using var stream = new MemoryStream(content, writable: false);
        return AssemblyLoadContext.Default.LoadFromStream(stream);
    }
}
