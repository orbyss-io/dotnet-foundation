using System.Text.Json.Nodes;

namespace Orbyss.Foundation.Build;

/// <summary>Rejects literal values anywhere beneath an explicitly secret metadata node.</summary>
internal static class SettingsMetadataSafety
{
    /// <summary>Checks nested structural constraints without copying or encoding secret values.</summary>
    internal static void Check(JsonNode? node, bool secret, int depth = 0)
    {
        if (depth > 16) throw new InvalidDataException("Settings constraints exceed depth16.");
        if (node is JsonObject obj)
        {
            secret |= obj["secret"] is JsonValue flag && flag.TryGetValue<bool>(out var classified) && classified;
            foreach (var field in obj)
            {
                if (secret && field.Key is "default" or "example" or "examples" or "enum" or "const")
                    throw new InvalidDataException("Secret metadata cannot carry literal values or examples at any nesting depth.");
                Check(field.Value, secret, depth + 1);
            }
        }
        else if (node is JsonArray array)
            foreach (var item in array) Check(item, secret, depth + 1);
    }
}
