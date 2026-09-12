namespace Orbyss.Foundation.Json;

/// <summary>Configures a named profile using fixed admission presets and allowlisted code extensions.</summary>
public sealed class JsonProfileSettings
{
    /// <summary>Gets or sets strict-request or tolerant-response.</summary>
    public string Preset { get; set; } = "strict-request";
    /// <summary>Gets or sets the maximum input/output size, capped at 16 MiB.</summary>
    public int MaxBytes { get; set; } = 1_048_576;
    /// <summary>Gets or sets the maximum nesting depth, capped at 64.</summary>
    public int MaxDepth { get; set; } = 32;
    /// <summary>Gets or sets code-registered extension identities.</summary>
    public string[] Extensions { get; set; } = [];
}
