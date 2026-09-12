namespace Orbyss.Foundation.Web.HostedPages;

/// <summary>Models Vite backend integration entries without dynamic property walking.</summary>
public sealed class HostedViteChunk
{
    /// <summary>Gets the emitted file path.</summary>
    public required string File { get; init; }
    /// <summary>Gets the optional source path, never used as a serving path.</summary>
    public string? Src { get; init; }
    /// <summary>Gets the optional chunk name.</summary>
    public string? Name { get; init; }
    /// <summary>Gets optional chunk names.</summary>
    public string[] Names { get; init; } = [];
    /// <summary>Gets whether this is a build entry.</summary>
    public bool IsEntry { get; init; }
    /// <summary>Gets whether this is a dynamic entry.</summary>
    public bool IsDynamicEntry { get; init; }
    /// <summary>Gets static imported manifest keys.</summary>
    public string[] Imports { get; init; } = [];
    /// <summary>Gets dynamic imported manifest keys.</summary>
    public string[] DynamicImports { get; init; } = [];
    /// <summary>Gets emitted CSS paths.</summary>
    public string[] Css { get; init; } = [];
    /// <summary>Gets emitted dependent asset paths.</summary>
    public string[] Assets { get; init; } = [];
}
