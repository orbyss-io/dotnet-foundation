namespace Orbyss.Foundation.Web.ProblemDetails;

/// <summary>Projects the configured problem JSON byte budget into the existing ASP.NET writer.</summary>
public sealed class FoundationProblemResponseOptions
{
    /// <summary>The minimum byte capacity admitting the nonrecursive safe fallback.</summary>
    public const int MinimumBytes = 512;
    /// <summary>The minimum nesting capacity admitting the closed field-diagnostic envelope.</summary>
    public const int MinimumDepth = 3;
    /// <summary>The deployment byte budget; Json.AspNetCore supplies its problem-response profile value.</summary>
    public int MaxBytes { get; set; } = 65_536;
    /// <summary>The deployment nesting budget projected from the same problem-response profile.</summary>
    public int MaxDepth { get; set; } = 32;
    /// <summary>The admitted response strictness; only the tolerant-response preset is supported.</summary>
    public string Preset { get; set; } = Orbyss.Foundation.Json.JsonProfileKeys.TolerantResponse;
}
