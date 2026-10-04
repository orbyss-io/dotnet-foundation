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
        var versions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var package in Directory.EnumerateFiles(directory, "*.nupkg").Order())
        {
            hashes[Path.GetFileName(package)] = Convert.ToHexStringLower(
                SHA256.HashData(File.ReadAllBytes(package)));
            using var archive = ZipFile.OpenRead(package);
            var nuspec = archive.Entries.SingleOrDefault(item =>
                item.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException(
                    $"staged package '{Path.GetFileName(package)}' must contain exactly one nuspec.");
            string stagedPackageId;
            using (var nuspecStream = nuspec.Open())
            {
                var document = XDocument.Load(nuspecStream);
                var metadata = document.Descendants().Single(item => item.Name.LocalName == "metadata");
                stagedPackageId = metadata.Elements()
                    .Single(item => item.Name.LocalName == "id").Value;
                versions[stagedPackageId] = metadata.Elements().Single(item => item.Name.LocalName == "version").Value;
                if (!packageIds.Add(stagedPackageId))
                    throw new InvalidOperationException(
                        $"multiple staged packages have package id '{stagedPackageId}'.");
            }
            var descriptorEntry = archive.GetEntry("orbyss-foundation/feature.json");
            var legacyEntry = archive.GetEntry("program-kit/feature.json");
            if (descriptorEntry is not null && legacyEntry is not null)
            {
                using var canonical = descriptorEntry.Open();
                using var legacy = legacyEntry.Open();
                if (!SHA256.HashData(canonical).AsSpan().SequenceEqual(SHA256.HashData(legacy)))
                    throw new InvalidOperationException("canonical and legacy feature descriptors disagree.");
            }
            descriptorEntry ??= legacyEntry;
            var pending = new List<FeatureDescriptor>();
            if (descriptorEntry is not null)
            {
                using var descriptorDocument = JsonDocument.Parse(descriptorEntry.Open());
                var root = descriptorDocument.RootElement;
                var schema = root.GetProperty("schemaVersion").GetInt32();
                if (schema is not (1 or 2))
                    throw new InvalidOperationException("unsupported feature descriptor schema.");
                var packageId = root.GetProperty("packageId").GetString();
                if (!string.Equals(packageId, stagedPackageId, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("feature descriptor packageId differs from the staged package.");
                var entries = schema == 1 ? new[] { root } : root.GetProperty("features").EnumerateArray().ToArray();
                foreach (var entry in entries)
                {
                    var identity = entry.GetProperty("identity").GetString();
                    if (string.IsNullOrWhiteSpace(identity))
                        throw new InvalidOperationException("feature identity is empty.");
                    var dependencies = entry.GetProperty("featureDependencies").EnumerateArray().Select(item => item.GetString()!).ToArray();
                    var routes = entry.GetProperty("routes").EnumerateArray().Select(item => item.GetString()!).ToArray();
                    var compose = !entry.TryGetProperty("composeForOpenApi", out var flag) || flag.GetBoolean();
                    var coverage = !entry.TryGetProperty("requiresContractCoverage", out var required) || required.GetBoolean();
                    var prefix = entry.TryGetProperty("routePrefixConfigurationPath", out var configuration) ? configuration.GetString() : null;
                    var suffixes = entry.TryGetProperty("routeSuffixes", out var suffix) ? suffix.EnumerateArray().Select(item => item.GetString()!).ToArray() : null;
                    if (prefix is not null && (string.IsNullOrWhiteSpace(prefix) || suffixes is null || suffixes.Any(item => !item.StartsWith('/'))))
                        throw new InvalidOperationException("invalid publisher configurable route metadata.");
                    pending.Add(new FeatureDescriptor(identity, packageId!, packageId!, dependencies, routes, compose, coverage, prefix, suffixes));
                }
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
            foreach (var descriptor in pending)
            {
                if (!assemblies.ContainsKey(descriptor.PackageId))
                    throw new InvalidOperationException($"feature package '{descriptor.PackageId}' has no compatible assembly.");
                if (!descriptors.TryAdd(descriptor.Identity, descriptor))
                    throw new InvalidOperationException($"multiple staged packages claim feature identity '{descriptor.Identity}'.");
            }
        }
        foreach (var (identity, definition) in BuiltInFeatures.Definitions)
        {
            if (!packageIds.Contains(definition.PackageId) || descriptors.ContainsKey(identity))
                continue;
            if (versions[definition.PackageId] is not ("0.2.2" or "0.2.3"))
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
                    definition.Routes,
                    definition.ComposeForOpenApi,
                    RequiresContractCoverage: false));
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
