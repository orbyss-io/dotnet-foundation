namespace Orbyss.Foundation.Json.AspNetCore;

/// <summary>Binds immutable JSON profile definitions for one shell.</summary>
public sealed class FoundationJsonOptions
{
    /// <summary>Gets or sets named profile definitions.</summary>
    public Dictionary<string, JsonProfileSettings> Profiles { get; set; } = new(StringComparer.Ordinal)
    {
        [JsonProfileKeys.StrictRequest] = new() { MaxBytes = 2 * 1024 * 1024 },
        [JsonProfileKeys.TolerantResponse] = new() { Preset = JsonProfileKeys.TolerantResponse },
        [JsonProfileKeys.SuccessResponse] = new() { Preset = JsonProfileKeys.TolerantResponse },
        [JsonProfileKeys.ProblemResponse] = new() { Preset = JsonProfileKeys.TolerantResponse, MaxBytes = 64 * 1024 }
    };
    /// <summary>Gets or sets configured count limits for independently paged sections.</summary>
    public JsonPageOptions Paging { get; set; } = new();
}
