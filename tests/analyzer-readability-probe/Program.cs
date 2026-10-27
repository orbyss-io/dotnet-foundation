using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Orbyss.Foundation.Analyzers.Probe;

/// <summary>Runs independently compiled positive and negative source-readability controls.</summary>
internal static class Program
{
    /// <summary>Compiles each fixture before asserting its exact diagnostic multiset.</summary>
    public static async Task Main()
    {
        var selectedAnalyzer = new SourceReadabilityAnalyzer();
        foreach (var descriptor in selectedAnalyzer.SupportedDiagnostics)
        {
            if (!descriptor.CustomTags.Contains(WellKnownDiagnosticTags.NotConfigurable))
                throw new InvalidOperationException("A selected mandatory rule can be downgraded or suppressed.");
        }

        var cases = new (string Name, string Members, string[] Expected)[]
        {
            ("parameters", "void M(int a, int b, int c) {}", ["ORB1007"]),
            ("parameters-good", "void M(\n    int a,\n    int b,\n    int c\n) {}", []),
            ("arguments", "void M() { Take(1, 2, 3); }", ["ORB1007"]),
            ("short-split", "void M() { Take(1,\n 2); }", ["ORB1007"]),
            ("short-split-good", "void M() { Take(\n    1,\n    2\n); }", []),
            ("wrong-close-indent", "void M() { Take(\n    1,\n    2\n  ); }", ["ORB1007"]),
            ("nested", "void M() { Take(Get()); }", ["ORB1008"]),
            ("awaited", "async System.Threading.Tasks.Task M() { Take(await System.Threading.Tasks.Task.FromResult(1)); }", ["ORB1008"]),
            ("construction", "void M() { Take(new object()); }", ["ORB1008"]),
            ("target-new", "void M() { TakeObject(new()); }", ["ORB1008"]),
            ("array", "void M() { Take(new[] { 1 }); }", ["ORB1008"]),
            ("collection", "void M() { TakeArray([1]); }", ["ORB1008"]),
            ("anonymous", "void M() { Take(new { Value = 1 }); }", ["ORB1008"]),
            ("lambda", "void M() { TakeAction(() => Get()); }", ["ORB1008"]),
            ("static-lambda", "void M() { TakeAction(static () => { }); }", ["ORB1008"]),
            ("anonymous-method", "void M() { TakeAction(delegate { }); }", ["ORB1008"]),
            ("prepared", "void M() { var value = Get(); System.Action action = () => Get(); Take(value); TakeAction(action); TakeAction(M); }", []),
            ("nameof", "void M() { Take(nameof(System.String.ToString)); }", []),
            ("method-named-nameof", "int @nameof(int x) => x; void M() { Take(@nameof(1)); }", ["ORB1008"]),
            ("chain-first", "void M() { this.Next()\n    .Next(); }", ["ORB1009"]),
            ("chain-next", "void M() { this\n    .Next().Next(); }", ["ORB1009"]),
            ("chain-good", "void M() { this\n    .Next()\n    .Next(); }", []),
            ("chain-indent", "void M() { this\n.Next()\n.Next(); }", ["ORB1009"]),
            ("chain-closing-shareline", "void M() { this\n    .Next(\n    ).Next(); }", ["ORB1009"]),
            ("chain-unqualified-first", "void M() { Next()\n    .Next(); }", ["ORB1009"]),
            ("chain-unqualified-bad", "void M() { Next().Next(); }", ["ORB1009"]),
            ("chain-delegate-first", "void M() { System.Func<Sample> factory = Next; factory()\n    .Next(); }", ["ORB1009"]),
            ("chain-prepared-factory", "void M() { var created = Next(); created.Next(); }", []),
            ("chain-property", "void M() { this.Next().Self.Next(); }", ["ORB1009"]),
            ("chain-parentheses", "void M() { (this.Next()).Next(); }", ["ORB1009"]),
            ("prepared-parenthesized-receiver", "void M() { (\n    this\n)\n    .Next()\n    .Next(); }", []),
            ("parenthesized-receiver-shared-call", "void M() { (\n    this\n).Next()\n    .Next(); }", ["ORB1009"]),
            ("chain-wrapped", "void M() { var result = (this.Next().Next()); }", ["ORB1009"]),
            ("chain-final-property", "void M() { var result = this.Next().Next().Self; }", ["ORB1009"]),
            ("chain-conditional", "void M() { this?.Next().Next(); }", ["ORB1009"]),
            ("chain-conditional-wrapped", "void M() { var result = (this?.Next().Next()); }", ["ORB1009"]),
            ("chain-conditional-good", "void M() { this\n    ?.Next()\n    .Next(); }", []),
            ("chain-conditional-property-good", "void M() { this?.Self\n    .Next()\n    .Next(); }", []),
            ("single-call", "void M() { this.Next(); Next(); }", []),
            ("field-last", "void M() {} int value;", ["ORB1010"]),
            ("fields-first", "int value; const int Limit = 1; void M() {}", []),
        };
        foreach (var item in cases)
        {
            var source = "class Sample { " + item.Members + "\n"
                + "void Take(params object[] values) {} void TakeObject(object value) {} "
                + "void TakeArray(int[] value) {} void TakeAction(System.Action value) {} "
                + "int Get() => 1; Sample Next() => this; Sample Self => this; }";
            await Check(
                item.Name,
                source,
                item.Expected
            );
        }

        var specialCases = new (string Name, string Source, string[] Expected, bool Enabled, string Path)[]
        {
            ("primary-constructor", "class Sample(int a, int b, int c) {}", ["ORB1007"], true, "Sample.cs"),
            ("primary-good", "class Sample(\n    int a,\n    int b,\n    int c\n) {}", [], true, "Sample.cs"),
            ("delegate", "delegate void Handler(int a, int b, int c);", ["ORB1007"], true, "Sample.cs"),
            ("base-constructor", "class Parent { public Parent(int a, int b, int c) {} } class Sample : Parent { Sample() : base(1, 2, 3) {} }", ["ORB1007", "ORB1007"], true, "Sample.cs"),
            ("generated-header", "// <auto-generated/>\nclass Sample(int a, int b, int c) {}", [], true, "Sample.cs"),
            ("generated-file", "class Sample(int a, int b, int c) {}", [], true, "Sample.g.cs"),
            ("profile-unselected", "class Sample(int a, int b, int c) {}", [], false, "Sample.cs"),
            ("with-value", "record Sample(int Value) { void M(Sample value) { Take(value with { Value = 2 }); } void Take(Sample value) {} }", ["ORB1008"], true, "Sample.cs"),
            ("prepared-with-value", "record Sample(int Value) { void M(Sample value) { var prepared = value with { Value = 2 }; Take(prepared); } void Take(Sample value) {} }", [], true, "Sample.cs"),
            ("stackalloc-value", "class Sample { void M() { Take(stackalloc int[2]); } void Take(System.Span<int> value) {} }", ["ORB1008"], true, "Sample.cs"),
            ("implicit-stackalloc-value", "class Sample { void M() { Take(stackalloc[] { 1 }); } void Take(System.Span<int> value) {} }", ["ORB1008"], true, "Sample.cs"),
            ("prepared-stackalloc-value", "class Sample { void M() { System.Span<int> prepared = stackalloc int[2]; Take(prepared); } void Take(System.Span<int> value) {} }", [], true, "Sample.cs"),
        };
        var nativeMigration = "using Microsoft.EntityFrameworkCore.Migrations;\n"
            + "public partial class Initial : Migration { "
            + "protected override void Up(MigrationBuilder migrationBuilder) { "
            + "migrationBuilder.CreateTable(name: \"items\", schema: \"owned\", columns: table => new { "
            + "Id = table.Column<long>(type: \"bigint\", nullable: false)"
            + ".Annotation(\"Npgsql:ValueGenerationStrategy\", 1), "
            + "Value = table.Column<string>(type: \"text\", nullable: false) }, "
            + "constraints: table => { table.PrimaryKey(\"PK_items\", x => x.Id); }); } "
            + "protected override void Down(MigrationBuilder migrationBuilder) { "
            + "migrationBuilder.DropTable(name: \"items\", schema: \"owned\"); } }";
        var emptyDiagnostics = Array.Empty<string>();
        await Check(
            "native-ef-declarations-without-generated-header",
            nativeMigration,
            emptyDiagnostics
        );
        var authoredMigration = nativeMigration.Replace(
            "migrationBuilder.DropTable(name: \"items\", schema: \"owned\");",
            "migrationBuilder.Sql(GetSql()); } string GetSql() => \"select 1\"; void Authored() { Take(1, 2, 3); } void Take(params int[] values) {"
        );
        var authoredDiagnostics = new[] { "ORB1007", "ORB1008" };
        await Check(
            "authored-migration-code-remains-checked",
            authoredMigration,
            authoredDiagnostics
        );
        var arbitraryConstraint = nativeMigration.Replace(
            "table.PrimaryKey(\"PK_items\", x => x.Id);",
            "System.Console.WriteLine(\"authored work\"); table.PrimaryKey(\"PK_items\", x => x.Id);"
        );
        var constraintDiagnostics = new[] { "ORB1007", "ORB1008", "ORB1008" };
        await Check(
            "authored-work-in-native-declaration-remains-checked",
            arbitraryConstraint,
            constraintDiagnostics
        );
        var helperMigration = nativeMigration.Replace(
            "migrationBuilder.DropTable(name: \"items\", schema: \"owned\");",
            "Nested(); void Nested() { migrationBuilder.CreateIndex(\"ix\", \"items\", \"Id\"); }"
        );
        var listDiagnostics = new[] { "ORB1007" };
        await Check(
            "authored-local-function-remains-checked",
            helperMigration,
            listDiagnostics
        );
        var conditionalMigration = nativeMigration.Replace(
            "table.PrimaryKey(\"PK_items\", x => x.Id);",
            "if (System.DateTime.UtcNow.Ticks > 0) { } table.PrimaryKey(\"PK_items\", x => x.Id);"
        );
        await Check(
            "authored-conditional-in-constraint-remains-checked",
            conditionalMigration,
            constraintDiagnostics
        );
        var incrementMigration = nativeMigration.Replace(
            "table.PrimaryKey(\"PK_items\", x => x.Id);",
            "int counter = 0; counter++; table.PrimaryKey(\"PK_items\", x => x.Id);"
        );
        await Check(
            "authored-increment-in-constraint-remains-checked",
            incrementMigration,
            constraintDiagnostics
        );
        var ordinaryHelper = "using Microsoft.EntityFrameworkCore.Migrations; "
            + "class Sample { void Up(MigrationBuilder builder) { builder.CreateIndex(\"ix\", \"items\", \"Id\"); } }";
        await Check(
            "ordinary-helper-named-up-remains-checked",
            ordinaryHelper,
            listDiagnostics
        );
        var fakeEf = "namespace Microsoft.EntityFrameworkCore.Migrations { "
            + "abstract class Migration { protected abstract void Up(MigrationBuilder builder); } "
            + "class MigrationBuilder { public void CreateIndex(params string[] values) {} } "
            + "class Sample : Migration { protected override void Up(MigrationBuilder builder) { "
            + "builder.CreateIndex(\"ix\", \"items\", \"Id\"); } } }";
        await Check(
            "fake-ef-symbols-remain-checked",
            fakeEf,
            listDiagnostics
        );
        foreach (var item in specialCases)
        {
            await Check(
                item.Name,
                item.Source,
                item.Expected,
                item.Enabled,
                item.Path
            );
        }

        var ownedSources = new[]
        {
            "src/Orbyss.Foundation.Analyzers/SourceReadabilityAnalyzer.cs",
            "src/Orbyss.Foundation.Analyzers/EfMigrationLayout.cs",
            "src/Orbyss.Foundation.Analyzers/FoundationAnalyzer.cs",
            "src/Orbyss.Foundation.Analyzers/SelectedFoundationAnalyzer.cs",
            "src/Orbyss.Foundation.Analyzers/SourceSuppressionPolicyGenerator.cs",
            "tests/analyzer-readability-probe/Program.cs",
            "tests/analyzer-readability-probe/ProbeOptions.cs",
            "tests/analyzer-readability-probe/ProbeOptionsProvider.cs",
            "tests/analyzer-readability-probe/ExistingPolicyControls.cs",
        };
        var noDiagnostics = Array.Empty<string>();
        var ownedTexts = new List<string>();
        foreach (var path in ownedSources)
        {
            var implementation = File.ReadAllText(path);
            var source = "global using System; global using System.Linq; global using System.Collections.Generic; global using System.Threading.Tasks; global using System.IO;\n" + implementation;
            ownedTexts.Add(source);
        }

        var firstSource = ownedTexts[0];
        ownedTexts.RemoveAt(0);
        var companionSources = ownedTexts.ToArray();
        await Check(
            "implementation-source",
            firstSource,
            noDiagnostics,
            companionSources: companionSources
        );

        Console.WriteLine($"Source-readability controls passed ({cases.Length + specialCases.Length + 9} separately compiled cases).");
        await ExistingPolicyControls.Run();
    }

    /// <summary>Rejects compiler failures separately from intended analyzer failures.</summary>
    private static async Task Check(
        string name,
        string source,
        string[] expected,
        bool enabled = true,
        string path = "Sample.cs",
        string[]? companionSources = null
    )
    {
        var tree = CSharpSyntaxTree.ParseText(source, path: path);
        var platformPaths = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        var referencePaths = platformPaths.Split(Path.PathSeparator);
        var references = referencePaths.Select(CreateReference);
        var trees = new List<SyntaxTree> { tree };
        foreach (var companion in companionSources ?? [])
        {
            var companionTree = CSharpSyntaxTree.ParseText(companion);
            trees.Add(companionTree);
        }
        var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
        var compilation = CSharpCompilation.Create(
            name,
            trees,
            references,
            compilationOptions
        );
        Func<Diagnostic, bool> isError = diagnostic => diagnostic.Severity == DiagnosticSeverity.Error;
        var errors = compilation
            .GetDiagnostics()
            .Where(isError)
            .ToArray();
        if (errors.Length != 0)
        {
            var details = string.Join<Diagnostic>(Environment.NewLine, errors);
            var message = $"{name}: fixture compiler failure: {details}";
            throw new InvalidOperationException(message);
        }

        var additionalFiles = ImmutableArray<AdditionalText>.Empty;
        var provider = new ProbeOptionsProvider(enabled);
        var options = new AnalyzerOptions(additionalFiles, provider);
        var analyzer = new SourceReadabilityAnalyzer();
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(analyzer);
        var analyzed = compilation.WithAnalyzers(analyzers, options);
        var diagnostics = await analyzed.GetAnalyzerDiagnosticsAsync();
        Func<Diagnostic, string> getId = diagnostic => diagnostic.Id;
        var actual = diagnostics
            .Select(getId)
            .Order()
            .ToArray();
        var expectedIds = expected.Order();
        if (!actual.SequenceEqual(expectedIds))
        {
            var expectedList = string.Join(",", expected);
            var actualList = string.Join(",", actual);
            var details = string.Join<Diagnostic>(Environment.NewLine, diagnostics);
            var message = $"{name}: expected [{expectedList}], actual [{actualList}]: {details}";
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>Creates one metadata reference without burying work in the Select argument.</summary>
    private static PortableExecutableReference CreateReference(string path) => MetadataReference.CreateFromFile(path);
}
