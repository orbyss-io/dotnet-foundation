using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;

namespace Orbyss.Foundation.OpenApiExport;

/// <summary>Indexes feature metadata and managed assemblies from a staged NuGet package closure.</summary>
internal sealed class PackageSet
{
    /// <summary>Creates an immutable view of one validated extraction.</summary>
    private PackageSet(
        Dictionary<string, FeatureDescriptor> descriptors,
        Dictionary<string, byte[]> assemblies,
        Dictionary<string, string> hashes)
    {
        FeatureDescriptors = descriptors;
        Assemblies = assemblies;
        Hashes = hashes;
    }

    /// <summary>Gets feature descriptors keyed by exact Orbyss Foundation feature identity.</summary>
    public IReadOnlyDictionary<string, FeatureDescriptor> FeatureDescriptors { get; }

    /// <summary>Gets extracted managed assemblies keyed by simple assembly name.</summary>
    public IReadOnlyDictionary<string, byte[]> Assemblies { get; }

    /// <summary>Gets staged package SHA-256 hashes keyed by package filename.</summary>
    public IReadOnlyDictionary<string, string> Hashes { get; }

    /// <summary>Validates and extracts the net10.0 assets required for endpoint composition.</summary>
    public static PackageSet Load(string directory)
    {
        var descriptors = new Dictionary<string, FeatureDescriptor>(StringComparer.Ordinal);
        var assemblies = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        var packageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var package in Directory.EnumerateFiles(directory, "*.nupkg").Order())
        {
            hashes[Path.GetFileName(package)] = Convert.ToHexStringLower(
                SHA256.HashData(File.ReadAllBytes(package)));
            using var archive = ZipFile.OpenRead(package);
            var nuspec = archive.Entries.SingleOrDefault(item =>
                item.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException(
                    $"staged package '{Path.GetFileName(package)}' must contain exactly one nuspec.");
            using (var nuspecStream = nuspec.Open())
            {
                var document = XDocument.Load(nuspecStream);
                var metadata = document.Descendants().Single(item => item.Name.LocalName == "metadata");
                var stagedPackageId = metadata.Elements()
                    .Single(item => item.Name.LocalName == "id").Value;
                if (!packageIds.Add(stagedPackageId))
                    throw new InvalidOperationException(
                        $"multiple staged packages have package id '{stagedPackageId}'.");
            }
            var descriptorEntry = archive.GetEntry("orbyss-foundation/feature.json");
            string? packageId = null;
            string? identity = null;
            string[] dependencies = [];
            string[] routes = [];
            if (descriptorEntry is not null)
            {
                using var descriptorDocument = JsonDocument.Parse(descriptorEntry.Open());
                var root = descriptorDocument.RootElement;
                packageId = root.GetProperty("packageId").GetString();
                identity = root.GetProperty("identity").GetString();
                dependencies = root.GetProperty("featureDependencies").EnumerateArray()
                    .Select(item => item.GetString()!).ToArray();
                routes = root.GetProperty("routes").EnumerateArray()
                    .Select(item => item.GetString()!).ToArray();
            }
            var compatible = archive.Entries
                .Where(item => item.FullName.StartsWith("lib/", StringComparison.OrdinalIgnoreCase) &&
                               item.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                .Select(item => new { Entry = item, Rank = FrameworkRank(item.FullName) })
                .Where(item => item.Rank >= 0)
                .GroupBy(item => Path.GetFileNameWithoutExtension(item.Entry.FullName), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderBy(item => item.Rank).ThenBy(item => item.Entry.FullName).First().Entry);
            foreach (var entry in compatible)
            {
                using var stream = entry.Open();
                using var memory = new MemoryStream();
                stream.CopyTo(memory);
                var name = Path.GetFileNameWithoutExtension(entry.FullName);
                var content = memory.ToArray();
                if (assemblies.TryGetValue(name, out var existing) &&
                    !SHA256.HashData(existing).AsSpan().SequenceEqual(SHA256.HashData(content)))
                {
                    throw new InvalidOperationException(
                        $"staged packages contain conflicting assemblies named '{name}'.");
                }
                assemblies[name] = content;
            }
            if (identity is null || packageId is null)
                continue;
            if (!assemblies.ContainsKey(packageId))
                throw new InvalidOperationException(
                    $"feature package '{packageId}' has no lib/net10.0/{packageId}.dll assembly.");
            if (!descriptors.TryAdd(
                    identity,
                    new FeatureDescriptor(identity, packageId, packageId, dependencies, routes)))
            {
                throw new InvalidOperationException(
                    $"multiple staged packages claim feature identity '{identity}'.");
            }
        }
        foreach (var (identity, definition) in BuiltInFeatures.Definitions)
        {
            if (!packageIds.Contains(definition.PackageId) || descriptors.ContainsKey(identity))
                continue;
            if (!assemblies.ContainsKey(definition.PackageId))
                throw new InvalidOperationException(
                    $"built-in feature package '{definition.PackageId}' has no compatible managed assembly.");
            descriptors.Add(
                identity,
                new FeatureDescriptor(
                    identity,
                    definition.PackageId,
                    definition.PackageId,
                    definition.Dependencies,
                    definition.Routes));
        }
        return new PackageSet(descriptors, assemblies, hashes);
    }

    /// <summary>Ranks managed library assets that a net10.0 exporter can consume.</summary>
    private static int FrameworkRank(string path)
    {
        var framework = path.Split('/').Skip(1).FirstOrDefault()?.ToLowerInvariant();
        return framework switch
        {
            "net10.0" => 0,
            "net9.0" => 1,
            "net8.0" => 2,
            "net7.0" => 3,
            "net6.0" => 4,
            "netstandard2.1" => 5,
            "netstandard2.0" => 6,
            _ => -1,
        };
    }
}
