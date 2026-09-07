using System.Text.Json;
using CShells;

namespace Orbyss.Foundation.OpenApiExport;

/// <summary>Captures the activated feature set and effective HTTP prefix of one configured shell.</summary>
internal sealed record Shell(string[] Features, string RoutePrefix, ShellSettings Settings)
{
    /// <summary>Reads one shell without activating it or executing its lifecycle initializers.</summary>
    public static Shell Read(JsonElement root, string name)
    {
        if (!root.GetProperty("CShells").GetProperty("Shells").TryGetProperty(name, out var shell))
            throw new InvalidOperationException($"configured shell '{name}' does not exist.");
        var features = shell.GetProperty("Features").EnumerateObject()
            .Where(item => item.Value.ValueKind != JsonValueKind.False)
            .Select(item => item.Name).ToArray();
        var routing = shell.TryGetProperty("Configuration", out var configuration) &&
                      configuration.TryGetProperty("WebRouting", out var webRouting)
            ? webRouting
            : default;
        var segments = new[] { Property(routing, "Path"), Property(routing, "RoutePrefix") }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim('/'));
        var route = string.Join('/', segments);
        var settings = new ShellSettings(new ShellId(name), features);
        if (shell.TryGetProperty("Configuration", out configuration))
            Flatten(configuration, "", settings.ConfigurationData);
        return new Shell(features, route.Length == 0 ? "" : "/" + route, settings);
    }

    /// <summary>Reads an optional string property from a routing object.</summary>
    private static string? Property(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
            ? value.GetString()
            : null;

    /// <summary>Converts JSON configuration to the colon-delimited keys consumed by ShellSettings.</summary>
    private static void Flatten(JsonElement element, string prefix, IDictionary<string, object> target)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
                Flatten(property.Value, Join(prefix, property.Name), target);
            return;
        }
        if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in element.EnumerateArray())
                Flatten(item, Join(prefix, (index++).ToString(System.Globalization.CultureInfo.InvariantCulture)), target);
            return;
        }
        target[prefix] = element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? "",
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.Null => "",
            _ => element.GetRawText(),
        };
    }

    /// <summary>Appends one configuration segment using the standard configuration delimiter.</summary>
    private static string Join(string prefix, string name) =>
        prefix.Length == 0 ? name : $"{prefix}:{name}";
}
