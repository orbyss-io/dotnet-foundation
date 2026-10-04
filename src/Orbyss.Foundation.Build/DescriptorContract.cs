using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Orbyss.Foundation.Build;

/// <summary>Validates the packaged descriptor schema and relationships between its entries.</summary>
internal static class DescriptorContract
{
    /// <summary>Lists the metadata fields supported for each feature.</summary>
    private static readonly string[] FeatureKeys = ["identity", "featureDependencies", "runtimeDependencies", "routes",
        "dormant", "composeForOpenApi", "requiresContractCoverage", "routePrefixConfigurationPath", "routeSuffixes",
        "dynamicRoutesFromConfiguration"];
    /// <summary>Lists the package-level metadata shared by both descriptor schemas.</summary>
    private static readonly string[] CommonKeys = ["schemaVersion", "packageId", "sourceSha256", "hostProvidedDependencies"];

    /// <summary>Reads descriptor JSON while rejecting duplicate properties that conceal conflicting values.</summary>
    internal static JsonObject Read(string text)
    {
        using var document = JsonDocument.Parse(text);
        UniqueProperties(document.RootElement);
        return JsonNode.Parse(text) as JsonObject ?? throw new InvalidDataException("Descriptor must be an object.");
    }

    /// <summary>Checks feature inventories, routes, source bindings and resolved host dependency declarations.</summary>
    internal static void Validate(JsonObject descriptor, string packageId)
    {
        if (descriptor["schemaVersion"] is not JsonValue schema || !schema.TryGetValue<int>(out var version) || version is not (1 or 2))
            throw new InvalidDataException("Descriptor schemaVersion must be 1 or 2.");
        if (Text(descriptor["packageId"], "packageId") != packageId)
            throw new InvalidDataException("Publisher descriptor package identity does not match the package.");
        var allowed = version == 1 ? CommonKeys.Concat(FeatureKeys.Take(7)) : CommonKeys.Concat(["features"]);
        Keys(descriptor, allowed, "descriptor");
        var features = version == 1 ? new[] { descriptor } : Objects(descriptor["features"], "features", true);
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var feature in features)
        {
            if (version == 2) Keys(feature, FeatureKeys, "feature");
            var identity = Text(feature["identity"], "identity");
            if (!Regex.IsMatch(identity, "^[A-Za-z][A-Za-z0-9_.-]*$", RegexOptions.CultureInvariant) || !identities.Add(identity))
                throw new InvalidDataException("Feature identities must be valid and unique.");
            Names(feature["featureDependencies"], "featureDependencies", required: true);
            Names(feature["runtimeDependencies"], "runtimeDependencies", required: feature.ContainsKey("runtimeDependencies"));
            Names(feature["routes"], "routes", required: true, routes: true);
            foreach (var flag in new[] { "dormant", "composeForOpenApi", "requiresContractCoverage", "dynamicRoutesFromConfiguration" })
                if (feature.ContainsKey(flag) && (feature[flag] is not JsonValue value || !value.TryGetValue<bool>(out _)))
                    throw new InvalidDataException($"Feature {flag} must be a boolean.");
            var prefix = feature.ContainsKey("routePrefixConfigurationPath");
            var suffixes = feature.ContainsKey("routeSuffixes");
            if (prefix != suffixes) throw new InvalidDataException("Configurable routes require both a prefix configuration path and route suffixes.");
            if (prefix)
            {
                Text(feature["routePrefixConfigurationPath"], "routePrefixConfigurationPath");
                Names(feature["routeSuffixes"], "routeSuffixes", required: true, routes: true);
            }
        }
        if (descriptor.ContainsKey("sourceSha256"))
        {
            if (descriptor["sourceSha256"] is not JsonObject sources) throw new InvalidDataException("sourceSha256 must be an object.");
            foreach (var source in sources)
            {
                var parts = source.Key.Split('/');
                if (string.IsNullOrWhiteSpace(source.Key) || Path.IsPathRooted(source.Key) || source.Key.Contains('\\') || source.Key.Contains(':')
                    || parts.Any(part => part is "" or "." or ".."))
                    throw new InvalidDataException("Source bindings must use paths within the publisher project.");
                if (!Regex.IsMatch(Text(source.Value, "sourceSha256"), "^[0-9a-f]{64}$", RegexOptions.CultureInvariant))
                    throw new InvalidDataException("Source bindings require lowercase SHA-256 values.");
            }
        }
        if (descriptor.ContainsKey("hostProvidedDependencies"))
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var dependency in Objects(descriptor["hostProvidedDependencies"], "hostProvidedDependencies", false))
            {
                Keys(dependency, ["packageId", "minimumVersion"], "host dependency");
                if (!names.Add(Text(dependency["packageId"], "host dependency packageId")))
                    throw new InvalidDataException("Host dependency package identities must be unique.");
                if (!Regex.IsMatch(Text(dependency["minimumVersion"], "minimumVersion"),
                    "^[0-9]+\\.[0-9]+\\.[0-9]+(?:\\.[0-9]+)?(?:-[0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*)?(?:\\+[0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*)?$", RegexOptions.CultureInvariant))
                    throw new InvalidDataException("Host dependencies require concrete NuGet minimum versions.");
            }
        }
    }

    /// <summary>Requires a concrete string without empty or surrounding whitespace.</summary>
    private static string Text(JsonNode? node, string label) => node is JsonValue value && value.TryGetValue<string>(out var text)
        && !string.IsNullOrWhiteSpace(text) && text == text.Trim() ? text : throw new InvalidDataException($"{label} must be a non-empty string without surrounding whitespace.");

    /// <summary>Rejects fields outside the supported schema at this location.</summary>
    private static void Keys(JsonObject value, IEnumerable<string> allowed, string label)
    {
        var names = allowed.ToHashSet(StringComparer.Ordinal);
        if (value.Any(item => !names.Contains(item.Key))) throw new InvalidDataException($"Unknown or misplaced {label} property.");
    }

    /// <summary>Reads an object inventory with its required minimum cardinality.</summary>
    private static JsonObject[] Objects(JsonNode? node, string label, bool nonempty)
    {
        if (node is not JsonArray array || (nonempty && array.Count == 0) || array.Any(item => item is not JsonObject))
            throw new InvalidDataException($"{label} must be an array of objects" + (nonempty ? " with at least one entry." : "."));
        return array.Select(item => item!.AsObject()).ToArray();
    }

    /// <summary>Checks unique dependency names or absolute routes.</summary>
    private static void Names(JsonNode? node, string label, bool required, bool routes = false)
    {
        if (node is null && !required) return;
        if (node is not JsonArray array) throw new InvalidDataException($"{label} must be an array.");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in array)
        {
            var text = Text(item, label);
            if (!names.Add(text) || (routes && !text.StartsWith('/')))
                throw new InvalidDataException($"{label} must contain unique" + (routes ? " absolute routes." : " values."));
        }
    }

    /// <summary>Rejects repeated properties throughout the original JSON document.</summary>
    private static void UniqueProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in element.EnumerateObject())
            {
                if (!keys.Add(item.Name)) throw new InvalidDataException("Descriptor JSON contains duplicate property names.");
                UniqueProperties(item.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) UniqueProperties(item);
    }
}
