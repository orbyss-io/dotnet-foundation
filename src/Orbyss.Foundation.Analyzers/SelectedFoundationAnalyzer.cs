using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Orbyss.Foundation.Analyzers;

/// <summary>Protects existing mandatory policies only within the explicitly selected source profile.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SelectedFoundationAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => FoundationAnalyzer.SelectedDiagnostics;

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(ConfigureCompilation);
    }

    /// <summary>Registers the single existing checker with immutable protected descriptors.</summary>
    private static void ConfigureCompilation(CompilationStartAnalysisContext context)
    {
        if (SourceReadabilityAnalyzer.IsSelected(context.Options))
            FoundationAnalyzer.RegisterRules(context, selected: true);
    }
}
