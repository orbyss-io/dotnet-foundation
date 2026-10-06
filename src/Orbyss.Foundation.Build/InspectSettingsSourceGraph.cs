using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Build.Framework;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Orbyss.Foundation.Build;

/// <summary>Inspects explicit foreign source snapshots without loading assemblies or claiming publisher authority.</summary>
public sealed class InspectSettingsSourceGraph : Microsoft.Build.Utilities.Task
{
    /// <summary>Explicit metadata-only source snapshots; never added to runtime Compile items.</summary>
    [Required] public ITaskItem[] SourceFiles { get; set; } = [];
    /// <summary>Native compiler references for type and constant resolution.</summary>
    public ITaskItem[] ReferenceFiles { get; set; } = [];
    /// <summary>Explicit fully qualified source types to inspect.</summary>
    [Required] public ITaskItem[] TypeNames { get; set; } = [];
    /// <summary>Explicit reviewed secret source properties, with TypeName and Property item metadata.</summary>
    public ITaskItem[] SecretProperties { get; set; } = [];
    /// <summary>Explicit native compiler imports bound by the caller's original project context.</summary>
    public ITaskItem[] GlobalUsings { get; set; } = [];
    /// <summary>Explicit nullable compilation context from the original native project properties.</summary>
    public string NullableContext { get; set; } = "disable";
    /// <summary>Bounded inspection output consumed by the independently qualified integration producer.</summary>
    [Required] public string OutputFile { get; set; } = "";

    /// <inheritdoc />
    public override bool Execute()
    {
        try
        {
            Require(SourceFiles.Length is > 0 and <= 128 && ReferenceFiles.Length <= 512 && TypeNames.Length is > 0 and <= 64,
                "Source graph inspection exceeds its finite input inventory.");
            var sources = new JsonObject();
            var trees = new List<SyntaxTree>();
            long total = 0;
            foreach (var source in SourceFiles)
            {
                var path = Path.GetFullPath(source.ItemSpec);
                var length = new FileInfo(path).Length;
                total += length;
                Require(length <= 1_048_576 && total <= 16_777_216, "Source graph snapshots exceed 1 MiB/file or 16 MiB total.");
                var text = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
                Require(!sources.ContainsKey(path), "Duplicate source graph snapshot.");
                sources.Add(path, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text))));
                var tree = CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(LanguageVersion.Latest), path);
                Require(!tree.GetDiagnostics().Any(item => item.Severity == DiagnosticSeverity.Error), "Invalid source graph snapshot.");
                Require(!tree.GetRoot().DescendantTrivia(descendIntoTrivia: true).Any(trivia => trivia.GetStructure() is
                    IfDirectiveTriviaSyntax or ElifDirectiveTriviaSyntax or ElseDirectiveTriviaSyntax or EndIfDirectiveTriviaSyntax
                    or DefineDirectiveTriviaSyntax or UndefDirectiveTriviaSyntax), "Conditional source graph semantics require compiler-specific qualification.");
                trees.Add(tree);
            }
            Require(ReferenceFiles.All(item => new FileInfo(item.ItemSpec).Length <= 67_108_864)
                && ReferenceFiles.Sum(item => new FileInfo(item.ItemSpec).Length) <= 268_435_456, "Source graph reference inventory exceeds limits.");
            Require(GlobalUsings.Length <= 32 && GlobalUsings.All(item => item.ItemSpec.Length <= 256
                && System.Text.RegularExpressions.Regex.IsMatch(item.ItemSpec, "^[A-Za-z_][A-Za-z0-9_]*(\\.[A-Za-z_][A-Za-z0-9_]*)*$")), "Invalid explicit native global-using context.");
            Require(NullableContext is "enable" or "disable", "Unsupported nullable compiler context.");
            if (GlobalUsings.Length > 0)
                trees.Add(CSharpSyntaxTree.ParseText(string.Join('\n', GlobalUsings.Select(item => "global using " + item.ItemSpec + ";"))));
            var compilation = CSharpCompilation.Create("SettingsSourceInspection", trees,
                ReferenceFiles.Select(item => MetadataReference.CreateFromFile(Path.GetFullPath(item.ItemSpec))),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                    nullableContextOptions: NullableContext == "enable" ? NullableContextOptions.Enable : NullableContextOptions.Disable));
            Require(SecretProperties.Length <= 256, "Source secret inventory exceeds 256 properties.");
            foreach (var secretItem in SecretProperties)
                Require(compilation.GetTypeByMetadataName(secretItem.GetMetadata("TypeName"))?.GetMembers(secretItem.GetMetadata("Property"))
                    .OfType<IPropertySymbol>().Any(property => property.DeclaredAccessibility == Accessibility.Public && !property.IsStatic) == true,
                    "Unknown declared source secret property.");
            var secretContracts = new JsonArray(SecretProperties.GroupBy(item => item.GetMetadata("TypeName"))
                .Select(group => (JsonNode?)new JsonObject { ["typeName"] = group.Key, ["settings"] = new JsonArray(group.Select(item =>
                    (JsonNode?)new JsonObject { ["property"] = item.GetMetadata("Property"), ["secret"] = true }).ToArray()) }).ToArray());
            var defaults = new SourceSettingsDefaults(compilation, secretContracts, [], [], []);
            var types = new JsonArray();
            foreach (var item in TypeNames)
            {
                var symbol = compilation.GetTypeByMetadataName(item.ItemSpec);
                Require(symbol is not null && symbol.DeclaringSyntaxReferences.Length > 0, "Inspection type is absent from selected source snapshots: " + item.ItemSpec);
                var properties = new JsonArray();
                if (symbol!.TypeKind == TypeKind.Enum)
                {
                    types.Add(new JsonObject { ["typeName"] = item.ItemSpec, ["kind"] = "enum", ["members"] = new JsonArray(symbol.GetMembers()
                        .OfType<IFieldSymbol>().Where(field => field.HasConstantValue).Select(field => (JsonNode?)JsonValue.Create(field.Name)).ToArray()),
                        ["memberValues"] = new JsonObject(symbol.GetMembers().OfType<IFieldSymbol>().Where(field => field.HasConstantValue)
                            .Select(field => KeyValuePair.Create<string, JsonNode?>(field.Name, JsonValue.Create(Convert.ToInt64(field.ConstantValue))))) });
                    continue;
                }
                Require(symbol.GetMembers().OfType<IPropertySymbol>().Count() <= 256, "Source type exceeds 256 property limit.");
                foreach (var property in symbol.GetMembers().OfType<IPropertySymbol>().Where(property => !property.IsStatic
                    && property.DeclaredAccessibility == Accessibility.Public && !property.IsImplicitlyDeclared))
                {
                    var syntax = property.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax()).SingleOrDefault();
                    var entry = new JsonObject { ["name"] = property.Name, ["type"] = defaults.SymbolKind(property.Type),
                        ["typeName"] = property.Type.ToDisplayString(), ["nullable"] = defaults.ImportedNullable(property), ["hasDefault"] = false };
                    foreach (var shape in defaults.ContainerShape(property.Type)) entry.Add(shape.Key, shape.Value?.DeepClone());
                    var nativeType = property.Type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
                        ? nullable.TypeArguments[0] : property.Type;
                    if (nativeType.ToDisplayString() is "System.TimeSpan" or "System.Uri")
                        entry["typeFormat"] = nativeType.ToDisplayString() == "System.TimeSpan" ? "timespan" : "uri";
                    var secret = SecretProperties.Any(secretItem => secretItem.GetMetadata("TypeName") == symbol.ToDisplayString()
                        && secretItem.GetMetadata("Property") == property.Name);
                    if (secret) entry["secret"] = true;
                    else if (syntax is PropertyDeclarationSyntax)
                    {
                        Require(!symbol.InstanceConstructors.Any(constructor => !constructor.IsImplicitlyDeclared),
                            "Constructed source settings need a separately qualified producer: " + item.ItemSpec);
                        entry["default"] = defaults.ReadSymbol(property);
                        entry["hasDefault"] = true;
                    }
                    else if (syntax is ParameterSyntax parameter && parameter.Default is not null)
                    {
                        var constant = compilation.GetSemanticModel(parameter.SyntaxTree).GetConstantValue(parameter.Default.Value);
                        Require(constant.HasValue && constant.Value is null, "Only explicit null positional record defaults are supported.");
                        entry["default"] = null;
                        entry["hasDefault"] = true;
                    }
                    properties.Add(entry);
                }
                types.Add(new JsonObject { ["typeName"] = item.ItemSpec, ["kind"] = symbol.IsRecord ? "record" : "object", ["properties"] = properties });
            }
            var output = new JsonObject { ["schemaVersion"] = 1, ["sourceSha256"] = sources, ["types"] = types,
                ["compilerContext"] = new JsonObject { ["nullable"] = NullableContext,
                    ["globalUsings"] = new JsonArray(GlobalUsings.Select(item => (JsonNode?)JsonValue.Create(item.ItemSpec)).ToArray()) } };
            var buffer = new BoundedJsonBuffer();
            buffer.Reset(2_097_151);
            using (var writer = new System.Text.Json.Utf8JsonWriter(buffer, new System.Text.Json.JsonWriterOptions { Indented = true }))
            {
                output.WriteTo(writer);
                writer.Flush();
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(OutputFile))!);
            File.WriteAllBytes(OutputFile, buffer.GetBytes(buffer.Length));
            return true;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or System.Text.Json.JsonException
            or ArgumentException or InvalidOperationException or OverflowException or NotSupportedException)
        {
            Log.LogError("PKSM002 unsupported native source settings graph: " + error.Message);
            return false;
        }
    }
    /// <summary>Rejects unsupported source inspection without replacing prior output.</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
