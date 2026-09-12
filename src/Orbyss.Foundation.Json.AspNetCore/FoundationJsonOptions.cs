namespace Orbyss.Foundation.Json.AspNetCore;

/// <summary>Binds immutable JSON profile definitions for one shell.</summary>
public sealed class FoundationJsonOptions
{
    /// <summary>Gets or sets named profile definitions.</summary>
    public Dictionary<string, JsonProfileSettings> Profiles { get; set; } = new(StringComparer.Ordinal)
    {
        ["strict-request"] = new(),
        ["tolerant-response"] = new() { Preset = "tolerant-response" }
    };
}
