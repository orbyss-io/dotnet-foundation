using Microsoft.CodeAnalysis.Diagnostics;

namespace Orbyss.Foundation.Analyzers.Probe;

/// <summary>Supplies the explicit compiler-visible profile selection for fixture compilations.</summary>
internal sealed class ProbeOptions(bool enabled) : AnalyzerConfigOptions
{
    /// <inheritdoc />
    public override bool TryGetValue(string key, out string value)
    {
        value = enabled ? "true" : "false";
        return key == "build_property.FoundationSourceReadability";
    }
}
