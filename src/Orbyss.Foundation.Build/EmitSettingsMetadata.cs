using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Build.Framework;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Orbyss.Foundation.Build;

/// <summary>Packs reviewed settings semantics with defaults read from syntax, without executing publisher code.</summary>
public sealed class EmitSettingsMetadata : Microsoft.Build.Utilities.Task
{
    /// <summary>Gets or sets the publisher declaration.</summary>
    [Required] public string DeclarationSource { get; set; } = "";
    /// <summary>Gets or sets the destination.</summary>
    [Required] public string OutputFile { get; set; } = "";
    /// <summary>Gets or sets the actual package identity.</summary>
    [Required] public string PackageId { get; set; } = "";
    /// <summary>Gets or sets the actual package version.</summary>
    [Required] public string PackageVersion { get; set; } = "";
    /// <summary>Gets or sets the publisher directory.</summary>
    [Required] public string ProjectDirectory { get; set; } = "";
    /// <summary>Gets or sets the compilation inventory.</summary>
    public ITaskItem[] SourceFiles { get; set; } = [];

    /// <summary>Gets or sets the compiled assembly bound by this contract.</summary>
    [Required] public string CompiledAssembly { get; set; } = "";
    /// <summary>Gets or sets whether packing must reuse the successfully compiled metadata.</summary>
    public bool ValidateOnly { get; set; }
    /// <inheritdoc />
    public override bool Execute()
    {
        try { Emit(); return true; }
        catch (Exception error) when (error is IOException or InvalidDataException or JsonException or InvalidOperationException or ArgumentException or NotSupportedException)
        {
            Log.LogError($"PKSM001 invalid publisher settings metadata: {error.Message}");
            return false;
        }
    }

    /// <summary>Validates all bindings before replacing output.</summary>
    private void Emit()
    {
        var declaration = DescriptorContract.Read(File.ReadAllText(DeclarationSource));
        Keys(declaration, ["schemaVersion", "packageId", "sourceSha256", "contracts"]);
        Require(declaration["schemaVersion"]?.GetValue<int>() == 1 && Text(declaration["packageId"]) == PackageId,
            "Settings declaration schema/package mismatch.");
        Require(!string.IsNullOrWhiteSpace(PackageVersion), "Package version is required.");
        var hashes = declaration["sourceSha256"] as JsonObject ?? throw new InvalidDataException("sourceSha256 is required.");
        var sources = new Dictionary<string, SyntaxNode>(StringComparer.Ordinal);
        var directory = Path.GetFullPath(ProjectDirectory);
        foreach (var item in SourceFiles)
        {
            var file = Path.GetFullPath(Path.Combine(directory, item.ItemSpec));
            var name = Path.GetRelativePath(directory, file).Replace('\\', '/');
            if (name.StartsWith("obj/", StringComparison.Ordinal)) continue;
            Require(!Path.IsPathRooted(name) && !name.Contains(':') && !name.Split('/').Any(part => part is "" or "." or ".."),
                "Settings sources must be contained in the publisher project.");
            for (var parent = new FileInfo(file) as FileSystemInfo; parent is not null && parent.FullName != directory;
                parent = parent is FileInfo info ? info.Directory : ((DirectoryInfo)parent).Parent)
                Require((parent.Attributes & FileAttributes.ReparsePoint) == 0, "Linked settings sources are unsupported.");
            var text = File.ReadAllText(file).Replace("\r\n", "\n", StringComparison.Ordinal);
            var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
            Require(hashes[name]?.GetValue<string>() == hash, $"Settings source binding changed: {name}. Review owning semantics.");
            var tree = CSharpSyntaxTree.ParseText(text);
            Require(!tree.GetDiagnostics().Any(item => item.Severity == DiagnosticSeverity.Error), $"Invalid C# settings source: {name}.");
            Require(sources.TryAdd(name, tree.GetRoot()), "Duplicate compilation source.");
        }
        Require(sources.Count > 0 && sources.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(hashes.Select(item => item.Key)),
            "Settings metadata must bind the exact compilation inventory.");
        var contracts = declaration["contracts"] as JsonArray ?? throw new InvalidDataException("contracts is required.");
        Require(contracts.Count > 0, "At least one explicitly scoped contract is required.");
        var outputContracts = new JsonArray();
        var scopes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in contracts)
        {
            var contract = node as JsonObject ?? throw new InvalidDataException("Contract must be an object.");
            Keys(contract, ["scope", "typeName", "complete", "settings", "semanticConstraints"]);
            var scope = Text(contract["scope"]);
            Require(scopes.Add(scope), "Duplicate contract scope.");
            var typeName = Text(contract["typeName"]);
            var candidates = sources.Values.SelectMany(root => root.DescendantNodes().OfType<ClassDeclarationSyntax>())
                .Where(type => QualifiedName(type) == typeName).ToArray();
            Require(candidates.Length == 1 && candidates[0].BaseList is null && !candidates[0].Modifiers.Any(SyntaxKind.PartialKeyword),
                "Settings type must be one non-inherited, non-partial class in the bound inventory.");
            var type = candidates[0];
            Require(!type.Ancestors().OfType<TypeDeclarationSyntax>().Any() && type.TypeParameterList is null,
                "Nested and generic settings classes are unsupported.");
            Require(!type.Members.OfType<FieldDeclarationSyntax>().Any(field => field.Modifiers.Any(SyntaxKind.PublicKeyword) && !field.Modifiers.Any(SyntaxKind.StaticKeyword)),
                "Public settings fields require a qualified owning exporter.");
            Require(type.ParameterList is null && !type.Members.OfType<ConstructorDeclarationSyntax>().Any(), "Constructed settings defaults require an owning exporter; no code is executed.");
            var properties = type.Members.OfType<PropertyDeclarationSyntax>()
                .Where(property => property.Modifiers.Any(SyntaxKind.PublicKeyword) && !property.Modifiers.Any(SyntaxKind.StaticKeyword)).ToDictionary(property => property.Identifier.ValueText);
            var settings = contract["settings"] as JsonArray ?? throw new InvalidDataException("settings is required.");
            var complete = Boolean(contract["complete"]);
            var declared = new HashSet<string>(StringComparer.Ordinal);
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var outputSettings = new JsonArray();
            foreach (var settingNode in settings)
            {
                var setting = settingNode as JsonObject ?? throw new InvalidDataException("Setting must be an object.");
                Keys(setting, ["property", "path", "required", "secret", "constraints", "binding", "precedence", "reload", "description"]);
                var name = Text(setting["property"]);
                Require(declared.Add(name) && properties.ContainsKey(name), "Duplicate or unknown settings property.");
                Require(paths.Add(Text(setting["path"])), "Duplicate configuration path.");
                _ = Boolean(setting["required"]);
                var secret = Boolean(setting["secret"]);
                _ = Text(setting["binding"]); _ = Text(setting["description"]);
                Require(Text(setting["reload"]) is "restart" or "reload" or "immutable", "Invalid reload semantics.");
                Strings(setting["precedence"], nonempty: true);
                var constraints = setting["constraints"] as JsonObject ?? throw new InvalidDataException("constraints must be an object.");
                Require(!secret || !constraints.Any(item => item.Key is "default" or "example" or "examples" or "enum" or "const"), "Secret settings cannot contain values or examples.");
                var property = properties[name];
                Require(property.AccessorList is not null && property.AccessorList.Accessors.All(accessor => accessor.Body is null && accessor.ExpressionBody is null),
                    "Computed settings require an owning exporter; no code is executed.");
                var kind = Kind(property.Type);
                var output = (JsonObject)setting.DeepClone();
                output.Remove("property"); output["type"] = kind;
                if (!secret) output["default"] = Default(property.Initializer?.Value, kind);
                outputSettings.Add(output);
            }
            Require(!complete || declared.SetEquals(properties.Keys), "Complete type contract must cover every public instance property.");
            Strings(contract["semanticConstraints"], nonempty: false);
            outputContracts.Add(new JsonObject { ["schemaVersion"] = 1, ["owner"] = PackageId, ["scope"] = scope,
                ["complete"] = complete, ["sources"] = hashes.DeepClone(), ["settings"] = outputSettings,
                ["semanticConstraints"] = contract["semanticConstraints"]!.DeepClone() });
        }
        var outputEnvelope = new JsonObject { ["schemaVersion"] = 1, ["packageId"] = PackageId, ["packageVersion"] = PackageVersion,
            ["sourceSha256"] = hashes.DeepClone(), ["contracts"] = outputContracts,
            ["assembly"] = new JsonObject { ["name"] = Path.GetFileName(CompiledAssembly),
                ["sha256"] = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(CompiledAssembly))) } };
        var payload = outputEnvelope.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";
        if (ValidateOnly)
        {
            Require(File.Exists(OutputFile) && File.ReadAllText(OutputFile) == payload,
                "Compiled settings metadata differs; rebuild the publisher before packing. --no-build cannot refresh provenance.");
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(OutputFile))!);
        File.WriteAllText(OutputFile, payload);
    }

    /// <summary>Finds an unambiguous namespace-qualified source type.</summary>
    private static string QualifiedName(ClassDeclarationSyntax type) => string.Join('.', type.Ancestors()
        .OfType<BaseNamespaceDeclarationSyntax>().Reverse().Select(item => item.Name.ToString()).Append(type.Identifier.ValueText));
    /// <summary>Supports a deliberately bounded set of primitive source types.</summary>
    private static string Kind(TypeSyntax type) => type.ToString() switch
    {
        "string" => "string", "bool" => "boolean", "int" or "long" => "integer", "double" or "float" or "decimal" => "number",
        "string[]" => "array", _ => throw new InvalidDataException("Unsupported settings source type; supply an owning exporter before claiming coverage.")
    };
    /// <summary>Reads literal initializers without loading a publisher assembly.</summary>
    private static JsonNode? Default(ExpressionSyntax? expression, string kind)
    {
        if (expression is null) return kind switch { "boolean" => JsonValue.Create(false), "integer" => JsonValue.Create(0), "number" => JsonValue.Create(0),
            _ => throw new InvalidDataException("Reference settings require an explicit supported initializer.") };
        if (kind == "string" && expression.ToString() == "string.Empty") return JsonValue.Create("");
        if (expression is CollectionExpressionSyntax collection && kind == "array")
        {
            var result = new JsonArray();
            foreach (var element in collection.Elements)
                result.Add(element is ExpressionElementSyntax item ? Default(item.Expression, "string") : throw new InvalidDataException("Spread settings defaults are unsupported."));
            return result;
        }
        if (expression is LiteralExpressionSyntax literal)
        {
            var value = literal.Token.Value;
            var valid = kind switch { "string" => value is string, "boolean" => value is bool, "integer" => value is int or long,
                "number" => value is int or long or float or double or decimal, _ => false };
            Require(valid, "Default literal differs from source type.");
            return JsonSerializer.SerializeToNode(value);
        }
        throw new InvalidDataException("Unsupported settings initializer; no publisher code is executed and defaults cannot be supplied by hand.");
    }
    /// <summary>Rejects missing, extra, and misplaced declaration fields.</summary>
    private static void Keys(JsonObject value, string[] fields) => Require(value.Select(item => item.Key).ToHashSet(StringComparer.Ordinal).SetEquals(fields), "Declaration fields differ from the versioned contract.");
    /// <summary>Requires concrete reviewed text.</summary>
    private static string Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) && text == text.Trim()
        ? text : throw new InvalidDataException("Nonempty trimmed text is required.");
    /// <summary>Requires actual booleans.</summary>
    private static bool Boolean(JsonNode? node) => node is JsonValue value && value.TryGetValue<bool>(out var flag) ? flag : throw new InvalidDataException("Boolean is required.");
    /// <summary>Checks textual semantic inventories.</summary>
    private static void Strings(JsonNode? node, bool nonempty)
    {
        var array = node as JsonArray ?? throw new InvalidDataException("String array is required.");
        Require(!nonempty || array.Count > 0, "Nonempty string array is required.");
        var values = array.Select(Text).ToArray();
        Require(values.Distinct(StringComparer.Ordinal).Count() == values.Length, "Duplicate string declaration.");
    }
    /// <summary>Reports bounded contract failures.</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
