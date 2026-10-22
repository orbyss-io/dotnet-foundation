using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Orbyss.Foundation.Analyzers.Probe;

/// <summary>Provides fixed compiler-visible profile options without reading a consumer environment.</summary>
internal sealed class ProbeOptionsProvider(bool enabled) : AnalyzerConfigOptionsProvider
{
    /// <inheritdoc />
    public override AnalyzerConfigOptions GlobalOptions { get; } = new ProbeOptions(enabled);

    /// <inheritdoc />
    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => GlobalOptions;

    /// <inheritdoc />
    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => GlobalOptions;
}
