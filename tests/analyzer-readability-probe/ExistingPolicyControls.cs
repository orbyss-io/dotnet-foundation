using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Orbyss.Foundation.Analyzers.Probe;

/// <summary>Checks mutually exclusive existing-policy production and native suppression semantics.</summary>
internal static class ExistingPolicyControls
{
    /// <summary>Compiles every selected/legacy and enabled/suppressed combination independently.</summary>
    internal static async Task Run()
    {
        foreach (var selected in new[] { true, false })
        {
            foreach (var suppressed in new[] { true, false })
                await Check(selected, suppressed);
        }
        Console.WriteLine("Existing policy profile controls passed (4 separately compiled exact diagnostic-count cases).");
    }

    /// <summary>Rejects compiler failure or duplicate old-policy producers before checking current protection.</summary>
    private static async Task Check(bool selected, bool suppressed)
    {
        var source = "namespace PolicyFixture; internal sealed class First { private static void Helper() {} internal static void Run() { Helper(); } } internal sealed class Second {}";
        var tree = CSharpSyntaxTree.ParseText(source);
        var platformPaths = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        var referencePaths = platformPaths.Split(Path.PathSeparator);
        var references = referencePaths.Select(CreateReference);
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
        if (suppressed)
        {
            var suppressedOptions = new Dictionary<string, ReportDiagnostic>
            {
                ["ORB1001"] = ReportDiagnostic.Suppress,
                ["ORB1003"] = ReportDiagnostic.Suppress,
                ["ORB1004"] = ReportDiagnostic.Suppress,
                ["ORB1005"] = ReportDiagnostic.Suppress,
                ["ORB1006"] = ReportDiagnostic.Suppress,
            };
            options = options.WithSpecificDiagnosticOptions(suppressedOptions);
        }
        var trees = new[] { tree };
        var name = $"profile-{selected}-{suppressed}";
        var compilation = CSharpCompilation.Create(
            name,
            trees,
            references,
            options
        );
        Func<Diagnostic, bool> isError = diagnostic => diagnostic.Severity == DiagnosticSeverity.Error;
        var compilerErrors = compilation
            .GetDiagnostics()
            .Where(isError)
            .ToArray();
        if (compilerErrors.Length != 0)
        {
            var details = string.Join<Diagnostic>(Environment.NewLine, compilerErrors);
            throw new InvalidOperationException(details);
        }
        var legacy = new FoundationAnalyzer();
        var selectedPolicy = new SelectedFoundationAnalyzer();
        var layout = new SourceReadabilityAnalyzer();
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(
            legacy,
            selectedPolicy,
            layout
        );
        var provider = new ProbeOptionsProvider(selected);
        var additionalFiles = ImmutableArray<AdditionalText>.Empty;
        var analyzerOptions = new AnalyzerOptions(additionalFiles, provider);
        var analyzed = compilation.WithAnalyzers(analyzers, analyzerOptions);
        var diagnostics = await analyzed.GetAnalyzerDiagnosticsAsync();
        Func<Diagnostic, string> getId = diagnostic => diagnostic.Id;
        var actual = diagnostics
            .Select(getId)
            .Order()
            .ToArray();
        string[] expected = selected || !suppressed
            ? ["ORB1003", "ORB1004", "ORB1005", "ORB1005", "ORB1005", "ORB1005"]
            : [];
        if (!actual.SequenceEqual(expected))
        {
            var details = string.Join<Diagnostic>(Environment.NewLine, diagnostics);
            var message = $"{name}: duplicate, missing or incorrectly suppressed diagnostics: {details}";
            throw new InvalidOperationException(message);
        }
        foreach (var diagnostic in diagnostics)
        {
            var protectedRule = diagnostic.Descriptor.CustomTags.Contains(WellKnownDiagnosticTags.NotConfigurable);
            if (protectedRule != selected)
                throw new InvalidOperationException("A diagnostic used the wrong compilation profile descriptor.");
        }
        foreach (var descriptor in selectedPolicy.SupportedDiagnostics)
        {
            if (descriptor.Id == "ORB1002" && (descriptor.DefaultSeverity != DiagnosticSeverity.Info
                || descriptor.CustomTags.Contains(WellKnownDiagnosticTags.NotConfigurable)))
            {
                throw new InvalidOperationException("ORB1002 lost its configurable Info recommendation contract.");
            }
        }
    }

    /// <summary>Prepares actual platform metadata without embedding calls into argument expressions.</summary>
    private static PortableExecutableReference CreateReference(string path) => MetadataReference.CreateFromFile(path);
}
