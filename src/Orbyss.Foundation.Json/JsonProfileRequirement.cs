namespace Orbyss.Foundation.Json;

/// <summary>Declares supported contract capacity/strictness; deployment budgets stay in profile settings.</summary>
public sealed class JsonProfileRequirement
{
    /// <summary>Creates a supported interval and strictness requirement.</summary>
    public JsonProfileRequirement(string preset, int minimumBytes = 1, int maximumBytes = 16_777_216,
        int minimumDepth = 1, int maximumDepth = 64)
    {
        if (preset is not (JsonProfileKeys.StrictRequest or JsonProfileKeys.TolerantResponse)
            || minimumBytes < 1 || maximumBytes < minimumBytes || maximumBytes > 16_777_216
            || minimumDepth < 1 || maximumDepth < minimumDepth || maximumDepth > 64)
            throw new ArgumentException("JSON contract has invalid supported requirements.");
        Preset = preset; MinimumBytes = minimumBytes; MaximumBytes = maximumBytes;
        MinimumDepth = minimumDepth; MaximumDepth = maximumDepth;
    }
    /// <summary>Gets required strictness.</summary>
    public string Preset { get; }
    /// <summary>Gets minimum supported capacity.</summary>
    public int MinimumBytes { get; }
    /// <summary>Gets maximum supported capacity.</summary>
    public int MaximumBytes { get; }
    /// <summary>Gets minimum supported nesting depth.</summary>
    public int MinimumDepth { get; }
    /// <summary>Gets maximum supported nesting depth.</summary>
    public int MaximumDepth { get; }
    /// <summary>Rejects a configured profile outside supported guarantees.</summary>
    public void Validate(JsonProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Preset != Preset || profile.MaxBytes < MinimumBytes || profile.MaxBytes > MaximumBytes
            || profile.MaxDepth < MinimumDepth || profile.MaxDepth > MaximumDepth)
            throw new InvalidOperationException("Configured JSON profile does not satisfy its contract requirements.");
    }
}
