using System.Text.Json.Nodes;
using Microsoft.Build.Framework;
using NuGet.Packaging;

namespace Orbyss.Foundation.Build;

/// <summary>Derives runtime requirements from the selected, restored native dependency graph.</summary>
internal static class RuntimeDependencyGraph
{
    /// <summary>Tracks the independently included runtime asset families through graph edges.</summary>
    private const int Runtime = 1, Native = 2, Resource = 4;

    /// <summary>Retains runtime roots, including private dependencies, and their shared transitives.</summary>
    internal static IEnumerable<JsonObject> Read(string assetsFile, string framework, string runtimeIdentifier,
        string projectDirectory, ITaskItem[] projectReferences)
    {
        var assets = JsonNode.Parse(File.ReadAllText(assetsFile))?.AsObject()
            ?? throw new InvalidDataException("Restore assets must be an object.");
        var targets = Object(assets["targets"], "restore targets");
        var frameworks = Object(assets["project"]?["frameworks"], "project frameworks");
        if (string.IsNullOrEmpty(framework))
        {
            if (targets.Count != 1) throw new InvalidDataException("Select an exact framework/runtime target for feature metadata.");
            var soleTarget = targets.First().Key.Split('/');
            framework = soleTarget[0];
            if (string.IsNullOrEmpty(runtimeIdentifier) && soleTarget.Length == 2) runtimeIdentifier = soleTarget[1];
        }
        var candidates = frameworks.Where(item => item.Key == framework || item.Value?["targetAlias"]?.GetValue<string>() == framework).ToArray();
        if (candidates.Length != 1) throw new InvalidDataException($"Select an unambiguous project framework for {framework}.");
        var selected = candidates[0];
        var targetName = selected.Key + (string.IsNullOrEmpty(runtimeIdentifier) ? "" : "/" + runtimeIdentifier);
        var target = Object(targets[targetName], $"restore target {targetName}");
        var libraries = Object(assets["libraries"], "restore libraries");
        var frameworkData = Object(selected.Value, $"project framework {selected.Key}");
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in target)
        {
            var split = item.Key.LastIndexOf('/');
            if (split <= 0 || !names.TryAdd(item.Key[..split], item.Key))
                throw new InvalidDataException("Restore target contains invalid or ambiguous package identities.");
        }
        string Resolve(string name) => names.TryGetValue(name, out var key) ? key
            : throw new InvalidDataException($"Restore target is missing dependency {name}.");
        var development = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        bool IsDevelopment(string key)
        {
            if (!development.TryGetValue(key, out var value)) development[key] = value = DevelopmentPackage(assets, libraries, key);
            return value;
        }
        var pending = new Stack<(string Key, int Assets)>();
        foreach (var dependency in OptionalObject(frameworkData, "dependencies"))
        {
            var key = Resolve(dependency.Key);
            var metadata = Object(dependency.Value, $"direct dependency {dependency.Key}");
            var included = AssetFlags(metadata["include"]?.GetValue<string>() ?? "all")
                & ~AssetFlags(metadata["exclude"]?.GetValue<string>() ?? "none");
            if (included == 0) continue;
            if (Object(libraries[key], $"restore library {key}")["type"]?.GetValue<string>() == "package" && IsDevelopment(key)) continue;
            // suppressParent/PrivateAssets controls downstream NuGet exposure, never runtime use.
            pending.Push((key, included));
        }
        foreach (var reference in projectReferences)
        {
            if (reference.GetMetadata("ReferenceOutputAssembly").Equals("false", StringComparison.OrdinalIgnoreCase)
                || reference.GetMetadata("OutputItemType").Equals("Analyzer", StringComparison.OrdinalIgnoreCase)) continue;
            var path = Path.GetFullPath(Path.Combine(projectDirectory, reference.ItemSpec));
            var matches = target.Where(item => libraries[item.Key]?["type"]?.GetValue<string>() == "project"
                && libraries[item.Key]?["msbuildProject"] is JsonValue project
                && Path.GetFullPath(Path.Combine(projectDirectory, project.GetValue<string>())).Equals(path,
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)).ToArray();
            if (matches.Length != 1) throw new InvalidDataException($"Restore graph does not identify runtime project reference {reference.ItemSpec} exactly once.");
            var included = AssetFlags(string.IsNullOrEmpty(reference.GetMetadata("IncludeAssets")) ? "all" : reference.GetMetadata("IncludeAssets"))
                & ~AssetFlags(reference.GetMetadata("ExcludeAssets"));
            pending.Push((matches[0].Key, included));
        }
        var reachable = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        while (pending.TryPop(out var next))
        {
            var (key, included) = next;
            var previous = reachable.GetValueOrDefault(key);
            if ((previous | included) == previous) continue;
            reachable[key] = previous | included;
            Object(libraries[key], $"restore library {key}");
            var entry = Object(target[key], $"dependency {key}");
            foreach (var child in OptionalObject(entry, "dependencies")) pending.Push((Resolve(child.Key), included));
        }
        foreach (var (key, included) in reachable.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            if (libraries[key]?["type"]?.GetValue<string>() != "package") continue;
            var entry = Object(target[key], $"dependency {key}");
            var hasRuntime = (included & Runtime) != 0 && OptionalObject(entry, "runtime").Any(file => file.Key.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));
            var hasNative = (included & Native) != 0 && OptionalObject(entry, "native").Any(file => !file.Key.EndsWith("/_._", StringComparison.Ordinal));
            var hasResources = (included & Resource) != 0 && OptionalObject(entry, "resource").Count != 0;
            var hasTargets = OptionalObject(entry, "runtimeTargets").Any(file =>
                (file.Value?["assetType"]?.GetValue<string>() == "runtime" && (included & Runtime) != 0)
                || (file.Value?["assetType"]?.GetValue<string>() == "native" && (included & Native) != 0));
            if (!hasRuntime && !hasNative && !hasResources && !hasTargets) continue;
            if (IsDevelopment(key))
                throw new InvalidDataException($"Runtime dependency graph requires development-only package {key}. Correct the publisher dependency contract.");
            var split = key.LastIndexOf('/');
            yield return new JsonObject { ["packageId"] = key[..split], ["minimumVersion"] = key[(split + 1)..] };
        }
    }

    /// <summary>Reads development metadata from the integrity-bound archive, never mutable extracted metadata.</summary>
    private static bool DevelopmentPackage(JsonObject assets, JsonObject libraries, string key)
    {
        var library = Object(libraries[key], $"restore library {key}");
        var relative = library["path"]?.GetValue<string>() ?? throw new InvalidDataException($"Restore package path is missing for {key}.");
        var expectedHash = library["sha512"]?.GetValue<string>() ?? throw new InvalidDataException($"Restore package integrity is missing for {key}.");
        foreach (var folder in Object(assets["packageFolders"], "package folders"))
        {
            var root = Path.GetFullPath(folder.Key);
            var package = Path.GetFullPath(Path.Combine(root, relative));
            if (Path.IsPathRooted(relative) || Path.GetRelativePath(root, package).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(part => part == ".."))
                throw new InvalidDataException($"Restore package path escapes its package folder: {key}.");
            if (!Directory.Exists(package)) continue;
            var archives = Directory.GetFiles(package, "*.nupkg");
            if (archives.Length != 1) throw new InvalidDataException($"Expected one restored package archive for {key}.");
            using var archive = new PackageArchiveReader(archives[0]);
            if (archive.GetContentHash(CancellationToken.None) != expectedHash)
                throw new InvalidDataException($"Restored package content integrity differs from the restore graph: {key}.");
            var split = key.LastIndexOf('/');
            var nuspec = archive.NuspecReader;
            if (!nuspec.GetId().Equals(key[..split], StringComparison.OrdinalIgnoreCase)
                || !nuspec.GetVersion().ToNormalizedString().Equals(key[(split + 1)..], StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Restored nuspec identity differs from {key}.");
            return nuspec.GetDevelopmentDependency();
        }
        throw new InvalidDataException($"Restored package metadata is unavailable for {key}.");
    }

    /// <summary>Expands the runtime asset families admitted by native IncludeAssets/ExcludeAssets.</summary>
    private static int AssetFlags(string raw)
    {
        var result = 0;
        foreach (var value in raw.Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            result |= value.ToLowerInvariant() switch { "all" => Runtime | Native | Resource, "runtime" => Runtime, "native" => Native, "resource" => Resource, _ => 0 };
        return result;
    }

    /// <summary>Missing optional inventories are empty; malformed inventories remain invalid.</summary>
    private static JsonObject OptionalObject(JsonObject parent, string key) => parent.ContainsKey(key) ? Object(parent[key], key) : new JsonObject();

    /// <summary>Rejects incomplete restore facts rather than guessing a dependency graph.</summary>
    private static JsonObject Object(JsonNode? value, string label) => value as JsonObject
        ?? throw new InvalidDataException($"Missing or invalid {label}.");
}
