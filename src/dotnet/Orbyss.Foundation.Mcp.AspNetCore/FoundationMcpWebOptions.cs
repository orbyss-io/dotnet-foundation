namespace Orbyss.Foundation.Mcp.AspNetCore;

/// <summary>Configures the shared authenticated stateless MCP transport endpoint.</summary>
public sealed class FoundationMcpWebOptions
{
    /// <summary>Names the shell configuration section.</summary>
    public const string SectionName = "Foundation:Mcp";
    /// <summary>Gets or sets the fixed Streamable HTTP route.</summary>
    public string Route { get; set; } = "/orbyss-foundation/mcp";
    /// <summary>Gets or sets the policy; null or whitespace uses the host default policy.</summary>
    public string? Policy { get; set; }
}
