using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;

namespace Orbyss.Foundation.Build;

/// <summary>Writes the canonical versioned descriptor consumed by package tools.</summary>
public sealed class EmitFeatureDescriptor : Microsoft.Build.Utilities.Task
{
    /// <summary>Gets or sets the destination file.</summary>
    [Required] public string OutputFile { get; set; } = "";
    /// <summary>Gets or sets the feature identity.</summary>
    public string Identity { get; set; } = "";
    /// <summary>Gets or sets the NuGet package identity.</summary>
    [Required] public string PackageId { get; set; } = "";
    /// <summary>Gets or sets semicolon-separated feature dependencies.</summary>
    public string FeatureDependencies { get; set; } = "";
    /// <summary>Gets or sets semicolon-separated runtime package dependencies.</summary>
    public string RuntimeDependencies { get; set; } = "";
    /// <summary>Gets or sets semicolon-separated routes.</summary>
    public string Routes { get; set; } = "";
    /// <summary>Gets or sets whether this feature is dormant.</summary>
    public bool Dormant { get; set; }
    /// <summary>Gets or sets whether endpoint export composes the feature.</summary>
    public bool ComposeForOpenApi { get; set; } = true;
    /// <summary>Gets or sets an optional publisher-owned descriptor source.</summary>
    public string DescriptorSource { get; set; } = "";
    /// <summary>Gets or sets the resolved NuGet assets file used for private runtime facts.</summary>
    public string AssetsFile { get; set; } = "";
    /// <summary>Gets or sets the publisher project directory.</summary>
    public string ProjectDirectory { get; set; } = "";
    /// <summary>Gets or sets the compilation source inventory.</summary>
    public ITaskItem[] SourceFiles { get; set; } = [];
    /// <inheritdoc />
    public override bool Execute()
    {
        try { return ExecuteCore(); }
        catch (Exception error) when (error is IOException or InvalidDataException or JsonException or InvalidOperationException or ArgumentException)
        {
            Log.LogError($"PKFD001 invalid publisher descriptor: {error.Message}");
            return false;
        }
    }

    /// <summary>Produces metadata only after validating publisher declarations and source bindings.</summary>
    private bool ExecuteCore()
    {
        if ((string.IsNullOrWhiteSpace(Identity) && string.IsNullOrWhiteSpace(DescriptorSource)) || string.IsNullOrWhiteSpace(PackageId)
            || Values(Routes).Any(route => !route.StartsWith('/')))
        {
            Log.LogError("Invalid feature identity, package identity, or non-absolute route.");
            return false;
        }
        var descriptor = JsonNode.Parse(JsonSerializer.Serialize(new
        {
            schemaVersion = 1, identity = Identity, packageId = PackageId,
            featureDependencies = Values(FeatureDependencies), runtimeDependencies = Values(RuntimeDependencies),
            routes = Values(Routes), dormant = Dormant, composeForOpenApi = ComposeForOpenApi,
        }))!.AsObject();
        if (!string.IsNullOrEmpty(DescriptorSource))
        {
            descriptor = DescriptorContract.Read(File.ReadAllText(DescriptorSource));
            DescriptorContract.Validate(descriptor, PackageId);
            var schema = descriptor["schemaVersion"]?.GetValue<int>();
            if (schema is not (1 or 2) || descriptor["packageId"]?.GetValue<string>() != PackageId)
            {
                Log.LogError("Publisher descriptor schema/package identity is invalid.");
                return false;
            }
            var sources = descriptor["sourceSha256"]?.AsObject();
            if (sources is not null)
            {
                var directory = Path.GetFullPath(ProjectDirectory);
                var features = new HashSet<string>(StringComparer.Ordinal);
                var sourceInventory = new HashSet<string>(StringComparer.Ordinal);
                foreach (var item in SourceFiles)
                {
                    var file = Path.GetFullPath(Path.Combine(directory, item.ItemSpec));
                    var relative = Path.GetRelativePath(directory, file).Replace('\\', '/');
                    if (relative.StartsWith("obj/", StringComparison.Ordinal) || relative.StartsWith("../", StringComparison.Ordinal)) continue;
                    sourceInventory.Add(relative);
                    var text = File.ReadAllText(file).Replace("\r\n", "\n", StringComparison.Ordinal);
                    var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
                    if (sources[relative]?.GetValue<string>() != hash)
                    {
                        Log.LogError($"Publisher metadata source binding changed: {relative}. Review and update the owning descriptor.");
                        return false;
                    }
                    foreach (Match match in Regex.Matches(text, "\\[ShellFeature\\(\\s*(?:name:\\s*)?\"([^\"]+)\"")) features.Add(match.Groups[1].Value);
                }
                var entries = schema == 1 ? new[] { descriptor } : descriptor["features"]!.AsArray().Select(item => item!.AsObject());
                var identities = entries.Select(entry => entry["identity"]!.GetValue<string>()).ToArray();
                if (!sourceInventory.SetEquals(sources.Select(item => item.Key)))
                {
                    Log.LogError("Publisher metadata must bind the exact compilation source inventory.");
                    return false;
                }
                if (identities.Length != identities.Distinct(StringComparer.Ordinal).Count() || !features.SetEquals(identities))
                {
                    Log.LogError("Publisher metadata must describe every source ShellFeature identity exactly once.");
                    return false;
                }
            }
        }
        if (!string.IsNullOrEmpty(AssetsFile) && File.Exists(AssetsFile))
        {
            var assets = JsonNode.Parse(File.ReadAllText(AssetsFile))!.AsObject();
            var libraries = assets["libraries"]!.AsObject();
            var target = assets["targets"]!.AsObject().First().Value!.AsObject();
            var runtime = new JsonArray();
            foreach (var (key, value) in target.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                if (libraries[key]?["type"]?.GetValue<string>() != "package" || value?["runtime"] is not JsonObject files
                    || !files.Any(file => file.Key.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
                    continue;
                var split = key.LastIndexOf('/');
                runtime.Add(new JsonObject { ["packageId"] = key[..split], ["minimumVersion"] = key[(split + 1)..] });
            }
            descriptor["hostProvidedDependencies"] = runtime;
        }
        DescriptorContract.Validate(descriptor, PackageId);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(OutputFile))!);
        File.WriteAllText(OutputFile, descriptor.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
        return true;
    }

    /// <summary>Normalizes a semicolon-separated metadata list.</summary>
    private static string[] Values(string raw) => raw.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
}
