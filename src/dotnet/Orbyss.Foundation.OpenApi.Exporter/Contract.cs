using System.Text.Json;

namespace Orbyss.Foundation.OpenApiExport;

/// <summary>Defines the exact shell composition that owns one public OpenAPI contract.</summary>
internal sealed record Contract(
    string Identity,
    string DocumentName,
    string Shell,
    string ProducerVersion,
    string[] Features)
{
    /// <summary>Loads and validates the exporter-owned portion of a consumer contract record.</summary>
    public static Contract Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1)
            throw new InvalidOperationException("unsupported OpenAPI contract configuration schema.");
        var producer = root.GetProperty("producer");
        if (producer.GetProperty("kind").GetString() != "Orbyss.Foundation.OpenApi.Exporter")
            throw new InvalidOperationException("producer.kind must be Orbyss.Foundation.OpenApi.Exporter.");
        return new Contract(
            root.GetProperty("identity").GetString()!,
            root.GetProperty("documentName").GetString()!,
            root.GetProperty("shell").GetString()!,
            producer.GetProperty("version").GetString()!,
            root.GetProperty("features").EnumerateArray().Select(item => item.GetString()!).ToArray());
    }
}
