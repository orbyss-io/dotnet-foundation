using System.Globalization;
using System.Text.Json;

namespace Orbyss.Foundation.Host.Configuration;

/// <summary>Loads a protected settings file using a separate immutable exact-path binding contract.</summary>
public static class FoundationRuntimeSettings
{
    /// <summary>Names the environment variable selecting the protected settings file.</summary>
    public const string SettingsFileVariable = "FOUNDATION_RUNTIME_SETTINGS_FILE";
    /// <summary>Names the environment variable selecting the release-owned binding contract.</summary>
    public const string ContractFileVariable = "FOUNDATION_RUNTIME_SETTINGS_CONTRACT_FILE";
    /// <summary>Limits the bytes read from either configuration file.</summary>
    private const int MaximumBytes = 1_048_576;
    /// <summary>Limits the number of exact setting bindings and admitted leaves.</summary>
    private const int MaximumLeaves = 4096;

    /// <summary>Loads the optional overlay before environment and command-line overrides.</summary>
    public static IConfigurationBuilder AddFoundationRuntimeSettings(this IConfigurationBuilder builder)
    {
        var settingsFile = Environment.GetEnvironmentVariable(SettingsFileVariable);
        var contractFile = Environment.GetEnvironmentVariable(ContractFileVariable);
        if (string.IsNullOrWhiteSpace(settingsFile) && string.IsNullOrWhiteSpace(contractFile)) return builder;
        if (string.IsNullOrWhiteSpace(settingsFile) || string.IsNullOrWhiteSpace(contractFile))
            throw Invalid("Both runtime settings and binding contract files are required.");
        return builder.AddInMemoryCollection(Read(settingsFile, contractFile, builder.Build()));
    }

    /// <summary>Validates and flattens a protected overlay without emitting its paths or values.</summary>
    public static IReadOnlyDictionary<string, string?> Read(string settingsFile, string contractFile, IConfiguration canonical)
    {
        ArgumentNullException.ThrowIfNull(canonical);
        using var contract = ReadDocument(contractFile);
        using var settings = ReadDocument(settingsFile);
        var bindings = ReadBindings(contract.RootElement, canonical);
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var stringPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Flatten(settings.RootElement, string.Empty, values, stringPaths, bindings, canonical);
        foreach (var (path, value) in values)
        {
            if (!bindings.TryGetValue(path, out var binding)) throw Invalid("The runtime overlay contains an undeclared setting.");
            if (binding.Kind != "value" && value is not null && !stringPaths.Contains(path))
                throw Invalid("A protected runtime setting requires a JSON string.");
            if (binding.Kind == "https-origin" && !IsHttpsOrigin(value)) throw Invalid("A runtime endpoint must be an HTTPS origin.");
            if (binding.Kind == "https-uri" && !IsHttpsUri(value)) throw Invalid("A runtime endpoint must be a safe HTTPS URI.");
        }
        foreach (var (path, binding) in bindings)
            if (binding.Required && (!values.TryGetValue(path, out var value) || string.IsNullOrWhiteSpace(value)))
                throw Invalid("The runtime overlay is missing a required nonblank setting.");
        return values;
    }

    /// <summary>Validates the release-owned binding contract and its immutable-path boundaries.</summary>
    private static Dictionary<string, RuntimeSettingsBinding> ReadBindings(JsonElement root, IConfiguration canonical)
    {
        RequireObject(root);
        RequireProperties(root, ["schemaVersion", "componentId", "settings"]);
        if (!root.TryGetProperty("schemaVersion", out var version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number) || number != 1
            || !root.TryGetProperty("componentId", out var component) || component.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(component.GetString()) || !root.TryGetProperty("settings", out var settings)
            || settings.ValueKind != JsonValueKind.Array || settings.GetArrayLength() > MaximumLeaves)
            throw Invalid("The runtime settings binding contract is invalid.");
        var result = new Dictionary<string, RuntimeSettingsBinding>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in settings.EnumerateArray())
        {
            RequireObject(entry);
            RequireProperties(entry, ["path", "required", "kind"]);
            if (!entry.TryGetProperty("path", out var pathValue) || pathValue.ValueKind != JsonValueKind.String
                || !entry.TryGetProperty("required", out var required) || required.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
                || !entry.TryGetProperty("kind", out var kindValue) || kindValue.ValueKind != JsonValueKind.String)
                throw Invalid("The runtime settings binding contract is invalid.");
            var path = pathValue.GetString()!;
            var kind = kindValue.GetString()!;
            if (!AllowedPath(path, canonical) || kind is not ("secret" or "https-origin" or "https-uri" or "value")
                || !result.TryAdd(path, new(required.GetBoolean(), kind)))
                throw Invalid("The runtime settings binding contract contains a forbidden or repeated setting.");
        }
        return result;
    }

    /// <summary>Allows declared values while preserving shell activation and package loading identities.</summary>
    private static bool AllowedPath(string path, IConfiguration canonical)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 1024) return false;
        var parts = path.Split(':');
        if (parts.Any(part => string.IsNullOrWhiteSpace(part) || part.Contains("__", StringComparison.Ordinal))) return false;
        if (parts.Any(part => part.Equals("Nuplane", StringComparison.OrdinalIgnoreCase))) return false;
        if (!parts[0].Equals("CShells", StringComparison.OrdinalIgnoreCase)) return true;
        return parts.Length > 4 && parts[1].Equals("Shells", StringComparison.OrdinalIgnoreCase)
            && parts[3].Equals("Configuration", StringComparison.OrdinalIgnoreCase)
            && canonical.GetSection("CShells:Shells").GetChildren().Any(shell => shell.Key.Equals(parts[2], StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Normalizes nested JSON into exact configuration paths without ambiguous delimiters.</summary>
    private static void Flatten(JsonElement value, string prefix, IDictionary<string, string?> output, ISet<string> stringPaths,
        IReadOnlyDictionary<string, RuntimeSettingsBinding> bindings, IConfiguration canonical)
    {
        if (prefix.Length != 0 && (!AllowedStructure(prefix, canonical)
            || !bindings.Keys.Any(path => path.Equals(prefix, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(prefix + ':', StringComparison.OrdinalIgnoreCase))))
            throw Invalid("The runtime overlay contains an undeclared or forbidden structure.");
        if (value.ValueKind == JsonValueKind.Object)
        {
            RequireObject(value);
            if (prefix.Length != 0 && !value.EnumerateObject().Any()) throw Invalid("Empty runtime configuration objects are unsupported.");
            foreach (var property in value.EnumerateObject())
            {
                if (property.Name.Contains(':', StringComparison.Ordinal) || property.Name.Contains("__", StringComparison.Ordinal)
                    || string.IsNullOrWhiteSpace(property.Name)) throw Invalid("The runtime overlay contains an invalid key.");
                Flatten(property.Value, prefix.Length == 0 ? property.Name : prefix + ':' + property.Name, output, stringPaths, bindings, canonical);
            }
            return;
        }
        if (prefix.Length == 0) throw Invalid("The runtime overlay must be a JSON object.");
        if (value.ValueKind == JsonValueKind.Array)
        {
            if (value.GetArrayLength() == 0) throw Invalid("Empty runtime configuration arrays are unsupported.");
            var index = 0;
            foreach (var item in value.EnumerateArray())
                Flatten(item, prefix + ':' + (index++).ToString(CultureInfo.InvariantCulture), output, stringPaths, bindings, canonical);
            return;
        }
        if (value.ValueKind == JsonValueKind.String) stringPaths.Add(prefix);
        if (output.Count >= MaximumLeaves || !output.TryAdd(prefix, value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Null => null,
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Number => value.GetRawText(),
                _ => throw Invalid("The runtime overlay contains an unsupported value.")
            })) throw Invalid("The runtime overlay contains repeated settings or exceeds its size limit.");
    }

    /// <summary>Validates containers before visiting leaves, including empty forbidden structures.</summary>
    private static bool AllowedStructure(string path, IConfiguration canonical)
    {
        var parts = path.Split(':');
        if (parts.Any(part => part.Equals("Nuplane", StringComparison.OrdinalIgnoreCase))) return false;
        if (!parts[0].Equals("CShells", StringComparison.OrdinalIgnoreCase)) return true;
        if (parts.Length == 1) return true;
        if (!parts[1].Equals("Shells", StringComparison.OrdinalIgnoreCase)) return false;
        if (parts.Length == 2) return true;
        if (!canonical.GetSection("CShells:Shells").GetChildren().Any(shell => shell.Key.Equals(parts[2], StringComparison.OrdinalIgnoreCase))) return false;
        return parts.Length == 3 || parts[3].Equals("Configuration", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Reads one bounded JSON file while excluding its contents from diagnostics.</summary>
    private static JsonDocument ReadDocument(string file)
    {
        if (!Path.IsPathFullyQualified(file)) throw Invalid("Runtime configuration files must use absolute paths.");
        try
        {
            using var stream = File.OpenRead(file);
            using var bytes = new MemoryStream();
            var buffer = new byte[8192];
            int count;
            while ((count = stream.Read(buffer)) != 0)
            {
                if (bytes.Length + count > MaximumBytes) throw Invalid("A runtime configuration file exceeds its size limit.");
                bytes.Write(buffer, 0, count);
            }
            return JsonDocument.Parse(bytes.ToArray(), new JsonDocumentOptions { MaxDepth = 32 });
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            throw Invalid("A runtime configuration file could not be read as bounded JSON.");
        }
    }

    /// <summary>Rejects non-object and case-insensitively repeated keys.</summary>
    private static void RequireObject(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object) throw Invalid("Runtime configuration must contain JSON objects.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in value.EnumerateObject())
            if (!names.Add(property.Name)) throw Invalid("Runtime configuration contains repeated object keys.");
    }

    /// <summary>Rejects fields outside the explicit binding contract shape.</summary>
    private static void RequireProperties(JsonElement value, IReadOnlyCollection<string> properties)
    {
        if (value.EnumerateObject().Any(property => !properties.Contains(property.Name, StringComparer.Ordinal)))
            throw Invalid("The runtime settings contract contains unsupported fields.");
    }

    /// <summary>Requires a noncredential HTTPS origin without a path or query.</summary>
    private static bool IsHttpsOrigin(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo)
        && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment) && uri.AbsolutePath == "/";

    /// <summary>Requires HTTPS without embedded credentials, query parameters or fragments.</summary>
    private static bool IsHttpsUri(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo)
        && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);

    /// <summary>Creates a value-free configuration rejection.</summary>
    private static InvalidDataException Invalid(string message) => new(message);
}
