using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Microsoft.Build.Framework;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Orbyss.Foundation.Build;

/// <summary>Reads a finite source subset; never loads or executes settings constructors.</summary>
internal sealed class SourceSettingsDefaults
{
    /// <summary>The publisher's actual compiler type and constant model.</summary>
    private readonly CSharpCompilation compilation;
    /// <summary>Validated defaults from independently emitted dependencies.</summary>
    private readonly Dictionary<string, Dictionary<string, JsonNode?>> imported = new(StringComparer.Ordinal);
    /// <summary>Property names classified as secrets by reviewed owning declarations.</summary>
    private readonly Dictionary<string, HashSet<string>> secrets = new(StringComparer.Ordinal);
    /// <summary>Imports referenced by real source defaults or profile contracts.</summary>
    private readonly HashSet<string> usedImports = new(StringComparer.Ordinal);
    /// <summary>Finite admitted graph-node count across the complete emission.</summary>
    private int nodes;
    /// <summary>Conservative retained graph-byte allowance before any repeated expansion.</summary>
    private int remainingBytes = 2_097_151;
    /// <summary>Portable dependency metadata and compiled-assembly provenance.</summary>
    internal JsonArray Imports { get; } = [];

    /// <summary>Validates explicit dependency metadata against native resolved implementation assemblies.</summary>
    internal SourceSettingsDefaults(CSharpCompilation compilation, JsonArray contracts, JsonArray imports,
        ITaskItem[] metadataFiles, ITaskItem[] implementationReferences)
    {
        this.compilation = compilation;
        Require(imports.Count <= 32 && metadataFiles.Length <= 32 && implementationReferences.Length <= 512,
            "Settings dependency inventory exceeds its finite limit.");
        foreach (var contract in contracts.OfType<JsonObject>())
        {
            var type = contract["typeName"]!.GetValue<string>();
            var names = contract["settings"]!.AsArray().OfType<JsonObject>()
                .Where(setting => setting["secret"]!.GetValue<bool>()).Select(setting => setting["property"]!.GetValue<string>());
            if (!secrets.TryGetValue(type, out var values)) secrets[type] = values = new(StringComparer.Ordinal);
            values.UnionWith(names);
        }
        var usedFiles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in imports.OfType<JsonObject>())
        {
            Require(item.Select(pair => pair.Key).ToHashSet(StringComparer.Ordinal).SetEquals(["typeName", "packageId", "scope"]),
                "Settings import fields differ from schema2.");
            var typeName = Text(item["typeName"]);
            var packageId = Text(item["packageId"]);
            var scope = Text(item["scope"]);
            var candidates = metadataFiles.Select(file => (Path: Path.GetFullPath(file.ItemSpec), Value: ReadMetadata(file.ItemSpec)))
                .Where(file => Text(file.Value["packageId"]) == packageId).ToArray();
            Require(candidates.Length == 1, "Settings import requires exactly one emitted dependency metadata file: " + packageId);
            var (path, metadata) = candidates[0];
            usedFiles.Add(path);
            Require(metadata["schemaVersion"]!.GetValue<int>() == 2, "Named type imports require dependency settings schema2; schema1 has no type authority.");
            var selected = metadata["contracts"]!.AsArray().OfType<JsonObject>().Where(contract => Text(contract["scope"]) == scope).ToArray();
            Require(selected.Length == 1 && selected[0]["complete"]!.GetValue<bool>() && Text(selected[0]["owner"]) == packageId
                && Text(selected[0]["typeName"]) == typeName,
                "Import requires one complete owning dependency scope.");
            var assembly = metadata["assembly"]!.AsObject();
            var assemblyName = Text(assembly["name"]);
            Require(Path.GetFileName(assemblyName) == assemblyName && assemblyName.EndsWith(".dll", StringComparison.Ordinal),
                "Dependency assembly identity must be a filename.");
            var binaries = implementationReferences.Select(file => Path.GetFullPath(file.ItemSpec))
                .Where(file => Path.GetFileName(file) == assemblyName).Distinct(StringComparer.Ordinal).ToArray();
            Require(binaries.Length == 1 && Hash(binaries[0]) == Text(assembly["sha256"]),
                "Dependency settings metadata differs from the actual resolved implementation assembly: " + packageId);
            var symbol = compilation.GetTypeByMetadataName(typeName);
            Require(symbol is not null && symbol.ContainingAssembly.Name == Path.GetFileNameWithoutExtension(assemblyName),
                "Imported type is not supplied by its resolved dependency assembly.");
            foreach (var child in metadata["contracts"]!.AsArray().OfType<JsonObject>())
            {
                var childType = Text(child["typeName"]);
                if (!secrets.TryGetValue(childType, out var classifications)) secrets[childType] = classifications = new(StringComparer.Ordinal);
                foreach (var field in child["settings"]!.AsArray().OfType<JsonObject>())
                {
                    SettingsMetadataSafety.Check(field["constraints"], field["secret"]!.GetValue<bool>());
                    if (field["secret"]!.GetValue<bool>())
                    {
                        Require(!field.ContainsKey("default"), "Imported secret settings cannot carry defaults, including null.");
                        classifications.Add(Text(field["path"]).Split(':')[^1]);
                    }
                }
            }
            var defaults = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
            var properties = symbol!.GetMembers().OfType<IPropertySymbol>().Where(property => property.DeclaredAccessibility == Accessibility.Public && !property.IsStatic).ToArray();
            var entries = selected[0]["settings"]!.AsArray().OfType<JsonObject>().ToArray();
            Require(properties.Select(property => property.Name).ToHashSet(StringComparer.Ordinal)
                .SetEquals(entries.Select(setting => Text(setting["path"]).Split(':')[^1])),
                "Imported scope does not match the compiler-resolved type's complete properties.");
            foreach (var setting in entries)
                if (!setting["secret"]!.GetValue<bool>())
                {
                    Require(setting.ContainsKey("default"), "Imported non-secret property lacks its source-backed default.");
                    var propertyName = Text(setting["path"]).Split(':')[^1];
                    var property = properties.Single(value => value.Name == propertyName);
                    Require(Text(setting["type"]) == Kind(property.Type), "Imported setting kind differs from its compiled property.");
                    Require(setting["default"] is not null || setting["constraints"]?["nullable"]?.GetValue<bool>() == true
                        && ImportedNullable(property), "Imported null default differs from its compiled nullable property.");
                    ReserveGraph(setting["default"], property.Type, 0);
                    // Clone only the charged, admitted value so its Parent chain cannot retain the input metadata DOM.
                    defaults.Add(propertyName, setting["default"]?.DeepClone());
                }
            Require(imported.TryAdd(typeName, defaults), "Duplicate settings type import.");
            Imports.Add(new JsonObject { ["typeName"] = typeName, ["packageId"] = packageId,
                ["packageVersion"] = Text(metadata["packageVersion"]), ["scope"] = scope,
                ["metadataSha256"] = Hash(path), ["assembly"] = assembly.DeepClone() });
        }
        Require(usedFiles.SetEquals(metadataFiles.Select(file => Path.GetFullPath(file.ItemSpec))),
            "Settings dependency metadata inventory must contain exactly the declared imports.");
    }

    /// <summary>Rejects unused imports that could imply unimplemented coverage.</summary>
    internal void VerifyImportsUsed() => Require(usedImports.SetEquals(imported.Keys), "Every settings import must participate in actual source defaults.");
    /// <summary>Finds an explicitly admitted external settings type.</summary>
    internal bool HasImportedType(string typeName) => imported.ContainsKey(typeName);
    /// <summary>Obtains the exact public property inventory of an imported compiler type.</summary>
    internal Dictionary<string, IPropertySymbol> ImportedProperties(string typeName) => compilation.GetTypeByMetadataName(typeName)!.GetMembers()
        .OfType<IPropertySymbol>().Where(property => property.DeclaredAccessibility == Accessibility.Public && !property.IsStatic)
        .ToDictionary(property => property.Name, StringComparer.Ordinal);
    /// <summary>Classifies the compiler-resolved property without executing it.</summary>
    internal string Kind(PropertyDeclarationSyntax property) => Kind(Type(property));
    /// <summary>Classifies an imported property from its native compiler symbol.</summary>
    internal string ImportedKind(IPropertySymbol property) => Kind(property.Type);
    /// <summary>Reads nullable source annotations, never a sample configuration value.</summary>
    internal bool IsNullable(PropertyDeclarationSyntax property) => Type(property).NullableAnnotation == NullableAnnotation.Annotated
        || Type(property) is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T };
    /// <summary>Reads nullable annotations retained in a referenced compiled type.</summary>
    internal bool ImportedNullable(IPropertySymbol property) => property.Type.NullableAnnotation == NullableAnnotation.Annotated
        || property.Type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T };
    /// <summary>Uses only the owning dependency's emitted non-secret default for a profile contract.</summary>
    internal JsonNode? ImportedDefault(string typeName, string propertyName)
    {
        usedImports.Add(typeName);
        Require(imported[typeName].ContainsKey(propertyName), "A profile cannot emit a default for an imported secret property.");
        var node = imported[typeName][propertyName];
        ReserveGraph(node, ImportedProperties(typeName)[propertyName].Type, 0);
        return node?.DeepClone();
    }
    /// <summary>Reads a local symbol for the neutral native source-inspection task.</summary>
    internal JsonNode? ReadSymbol(IPropertySymbol symbol)
    {
        var syntax = symbol.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax()).OfType<PropertyDeclarationSyntax>().SingleOrDefault();
        Require(syntax is not null, "Source inspection requires a bound auto-property declaration.");
        Require(syntax!.AccessorList is not null && syntax.AccessorList.Accessors.All(accessor => accessor.Body is null && accessor.ExpressionBody is null),
            "Computed settings accessors require a separately qualified producer.");
        return Read(syntax);
    }
    /// <summary>Describes the native source type kind for structural metadata.</summary>
    internal string SymbolKind(ITypeSymbol type) => Kind(type);
    /// <summary>Names actual compiler collection/dictionary element types for explicit nested bindings.</summary>
    internal JsonObject ContainerShape(ITypeSymbol type)
    {
        type = Unwrap(type);
        if (type is IArrayTypeSymbol array) return new JsonObject { ["elementTypeName"] = array.ElementType.ToDisplayString() };
        if (Collection(type) is { } element) return new JsonObject { ["elementTypeName"] = element.ToDisplayString() };
        if (type is INamedTypeSymbol named && Native(named, "System.Collections.Generic.Dictionary<TKey, TValue>"))
            return new JsonObject { ["keyTypeName"] = named.TypeArguments[0].ToDisplayString(), ["valueTypeName"] = named.TypeArguments[1].ToDisplayString() };
        return new JsonObject();
    }
    /// <summary>Reads the admitted source initializer into a finite JSON graph.</summary>
    internal JsonNode? Read(PropertyDeclarationSyntax property) => Value(property.Initializer?.Value, Type(property), property.SyntaxTree, 0);
    /// <summary>Resolves a local declared property through the actual compiler model.</summary>
    private ITypeSymbol Type(PropertyDeclarationSyntax property) => compilation.GetSemanticModel(property.SyntaxTree).GetDeclaredSymbol(property)!.Type;
    /// <summary>Preserves nullable semantics while classifying the underlying value type.</summary>
    private static ITypeSymbol Unwrap(ITypeSymbol type) => type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable ? nullable.TypeArguments[0] : type;
    /// <summary>Maps supported compiler types to schema2 JSON kinds.</summary>
    private static string Kind(ITypeSymbol type)
    {
        type = Unwrap(type);
        return type.SpecialType switch
        {
            SpecialType.System_String => "string", SpecialType.System_Boolean => "boolean",
            SpecialType.System_Int32 or SpecialType.System_Int64 => "integer",
            SpecialType.System_Double or SpecialType.System_Single or SpecialType.System_Decimal => "number",
            _ => type is IArrayTypeSymbol || Collection(type) is not null ? "array" : type.TypeKind == TypeKind.Enum
                || Native(type, "System.TimeSpan") || Native(type, "System.Uri") ? "string" : "object"
        };
    }
    /// <summary>Statically evaluates the supported defaults subset without invoking publisher methods.</summary>
    private JsonNode? Value(ExpressionSyntax? expression, ITypeSymbol type, SyntaxTree tree, int depth)
    {
        Require(++nodes <= 4096 && depth <= 16, "Settings graph exceeds 4096 nodes or depth16.");
        remainingBytes -= 32;
        Require(remainingBytes >= 0, "Settings graph exceeds the 2 MiB retained default budget.");
        var model = compilation.GetSemanticModel(tree);
        if (expression is null || expression.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.NullLiteralExpression))
        {
            if (type.NullableAnnotation == NullableAnnotation.Annotated || type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } || type.IsReferenceType) return null;
            return Kind(type) switch { "boolean" => JsonValue.Create(false), "integer" => JsonValue.Create(0), "number" => JsonValue.Create(0),
                "string" when Native(type, "System.TimeSpan") => JsonValue.Create(TimeSpan.Zero.ToString("c", CultureInfo.InvariantCulture)),
                _ => throw new InvalidDataException("Unsupported implicit source default.") };
        }
        type = Unwrap(type);
        var constant = model.GetConstantValue(expression);
        if (constant.HasValue)
        {
            Require(constant.Value is not string text || text.Length <= 16_384, "Settings default string exceeds 16 Ki-character limit.");
            if (constant.Value is string textValue)
            {
                remainingBytes -= checked(textValue.Length * 6);
                Require(remainingBytes >= 0, "Settings graph exceeds the 2 MiB retained default budget.");
            }
            if (type.TypeKind == TypeKind.Enum)
            {
                var field = model.GetSymbolInfo(expression).Symbol as IFieldSymbol;
                Require(field?.ContainingType.TypeKind == TypeKind.Enum, "Enum defaults must select a statically named member.");
                return JsonValue.Create(field!.Name);
            }
            return JsonSerializerNode(constant.Value);
        }
        if (type.SpecialType == SpecialType.System_String && expression.ToString() == "string.Empty") return JsonValue.Create("");
        if (type is IArrayTypeSymbol || Collection(type) is not null && expression is CollectionExpressionSyntax)
        {
            var arrayElement = type is IArrayTypeSymbol array ? array.ElementType : Collection(type)!;
            var elements = expression switch { CollectionExpressionSyntax collection => collection.Elements.Select(element =>
                element is ExpressionElementSyntax value ? value.Expression : throw new InvalidDataException("Spread settings defaults are unsupported.")),
                ArrayCreationExpressionSyntax created when created.Initializer is not null => created.Initializer.Expressions,
                ImplicitArrayCreationExpressionSyntax created => created.Initializer.Expressions,
                _ => throw new InvalidDataException("Unsupported source array initializer.") };
            var values = elements.ToArray();
            Require(values.Length <= 256, "Settings default array exceeds 256-item limit.");
            Require(!(Native(type, "System.Collections.Generic.HashSet<T>") || Native(type, "System.Collections.Generic.ISet<T>")) || values.Length == 0,
                "Nonempty set collection expressions require an explicit native constructor/comparer; no implicit set semantics are inferred.");
            return new JsonArray(values.Select(value => Value(value, arrayElement, tree, depth + 1)).ToArray());
        }
        if (Native(type, "System.TimeSpan") && expression is InvocationExpressionSyntax invocation)
        {
            var method = model.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            Require(method is not null && Native(method.ContainingType, "System.TimeSpan") && invocation.ArgumentList.Arguments.Count == 1,
                "Only statically bound single-argument TimeSpan factories are supported.");
            var argument = model.GetConstantValue(invocation.ArgumentList.Arguments[0].Expression);
            Require(argument.HasValue && argument.Value is int or long or double or float or decimal, "TimeSpan argument must be a compiler constant.");
            var value = Convert.ToDouble(argument.Value, CultureInfo.InvariantCulture);
            Require(double.IsFinite(value), "TimeSpan default must be finite.");
            var duration = method!.Name switch { "FromDays" => TimeSpan.FromDays(value), "FromHours" => TimeSpan.FromHours(value),
                "FromMinutes" => TimeSpan.FromMinutes(value), "FromSeconds" => TimeSpan.FromSeconds(value),
                "FromMilliseconds" => TimeSpan.FromMilliseconds(value), "FromTicks" => TimeSpan.FromTicks(Convert.ToInt64(argument.Value, CultureInfo.InvariantCulture)),
                _ => throw new InvalidDataException("Unsupported TimeSpan factory.") };
            return JsonValue.Create(duration.ToString("c", CultureInfo.InvariantCulture));
        }
        var initializer = expression switch { ObjectCreationExpressionSyntax created => created.Initializer,
            ImplicitObjectCreationExpressionSyntax created => created.Initializer, _ => null };
        var arguments = expression switch { ObjectCreationExpressionSyntax created => created.ArgumentList?.Arguments,
            ImplicitObjectCreationExpressionSyntax created => created.ArgumentList.Arguments, _ => null };
        Require(expression is ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax,
            "Unsupported settings initializer for " + type.ToDisplayString() + ": " + expression.Kind() + "; no publisher code is executed.");
        var constructedType = model.GetTypeInfo(expression).Type;
        var elementType = Collection(type);
        if (elementType is not null)
        {
            Require(constructedType is INamedTypeSymbol constructedCollection
                && (Native(constructedCollection, "System.Collections.Generic.List<T>")
                    || Native(constructedCollection, "System.Collections.Generic.HashSet<T>"))
                && SymbolEqualityComparer.Default.Equals(Collection(constructedCollection), elementType),
                "Settings collection initializer must construct the exact admitted native list/set element type; custom construction is unsupported.");
            Require(arguments is null || arguments.Value.Count == 0 || (arguments.Value.Count == 1
                && model.GetSymbolInfo(arguments.Value[0].Expression).Symbol is IPropertySymbol comparer
                && Native(comparer.ContainingType, "System.StringComparer") && comparer.Name is "Ordinal" or "OrdinalIgnoreCase"),
                "Settings collections support only parameterless or native ordinal comparer constructors.");
            Require(initializer is null || initializer.Expressions.Count <= 256, "Settings collection exceeds 256 items.");
            var values = (initializer?.Expressions ?? []).Select(item => Value(item, elementType, tree, depth + 1)).ToArray();
            if (constructedType is not null && Native(constructedType, "System.Collections.Generic.HashSet<T>"))
            {
                Require(elementType.SpecialType is SpecialType.System_String or SpecialType.System_Int32 or SpecialType.System_Int64
                    or SpecialType.System_Boolean, "Nonprimitive set defaults require a separately qualified equality exporter.");
                var ignoreCase = arguments is { } supplied && supplied.Count == 1
                    && model.GetSymbolInfo(supplied[0].Expression).Symbol is IPropertySymbol { Name: "OrdinalIgnoreCase" };
                for (var index = 0; index < values.Length; index++)
                    for (var previous = 0; previous < index; previous++)
                        Require(elementType.SpecialType == SpecialType.System_String
                            ? !string.Equals(values[index]?.GetValue<string>(), values[previous]?.GetValue<string>(),
                                ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
                            : !JsonNode.DeepEquals(values[index], values[previous]),
                            "Set default contains values removed by its native comparer; duplicate set initializers are unsupported.");
            }
            return new JsonArray(values);
        }
        if (type is INamedTypeSymbol dictionary && Native(dictionary, "System.Collections.Generic.Dictionary<TKey, TValue>"))
        {
            Require(SymbolEqualityComparer.Default.Equals(constructedType, type),
                "Settings dictionary initializer must construct its exact admitted native type.");
            Require(dictionary.TypeArguments[0].SpecialType == SpecialType.System_String, "Only string-key settings dictionaries are supported.");
            Require(arguments is null || arguments.Value.Count == 0 || (arguments.Value.Count == 1
                && model.GetSymbolInfo(arguments.Value[0].Expression).Symbol is IPropertySymbol comparer
                && Native(comparer.ContainingType, "System.StringComparer") && comparer.Name is "Ordinal" or "OrdinalIgnoreCase"),
                "Settings dictionary constructor must use a native ordinal comparer or no arguments.");
            var result = new JsonObject();
            var keyComparer = arguments is { } supplied && supplied.Count == 1
                && model.GetSymbolInfo(supplied[0].Expression).Symbol is IPropertySymbol { Name: "OrdinalIgnoreCase" }
                ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            var keys = new HashSet<string>(keyComparer);
            Require(initializer is null || initializer.Expressions.Count <= 256, "Settings dictionary exceeds 256 entries.");
            foreach (var entry in initializer?.Expressions ?? [])
            {
                Require(entry is AssignmentExpressionSyntax { Left: ImplicitElementAccessSyntax }, "Settings dictionary requires static index initializers.");
                var assignment = (AssignmentExpressionSyntax)entry;
                var key = model.GetConstantValue(((ImplicitElementAccessSyntax)assignment.Left).ArgumentList.Arguments.Single().Expression);
                Require(key.HasValue && key.Value is string { Length: > 0 and <= 4096 }, "Settings dictionary key must be a finite compiler constant.");
                Require(keys.Add((string)key.Value!), "Duplicate settings dictionary key under its native comparer.");
                result.Add((string)key.Value!, Value(assignment.Right, dictionary.TypeArguments[1], tree, depth + 1));
            }
            return result;
        }
        Require(arguments is null || arguments.Value.Count == 0, "Settings object constructor arguments require a qualified exporter.");
        Require(SymbolEqualityComparer.Default.Equals(constructedType, type),
            "Settings object initializer must construct its exact declared concrete type; derived/custom construction is unsupported.");
        var name = Nominal(type);
        JsonObject instance;
        if (imported.TryGetValue(name, out var defaults))
        {
            usedImports.Add(name);
            instance = new JsonObject();
            var properties = ImportedProperties(name);
            foreach (var value in defaults)
            {
                ReserveGraph(value.Value, properties[value.Key].Type, depth + 1);
                instance.Add(value.Key, value.Value?.DeepClone());
            }
        }
        else
        {
            var declarations = type.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax()).OfType<ClassDeclarationSyntax>().ToArray();
            Require(declarations.Length == 1, "Settings graph type requires bound local source or an owning metadata import: " + name);
            var source = declarations[0];
            Require(source.BaseList is null && source.ParameterList is null && !source.Members.OfType<ConstructorDeclarationSyntax>().Any()
                && !source.Members.OfType<FieldDeclarationSyntax>().Any(field => field.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PublicKeyword)
                    && !field.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StaticKeyword)
                    && !field.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.ConstKeyword)), "Constructed/inherited settings graphs are unsupported.");
            instance = new JsonObject();
            var properties = source.Members.OfType<PropertyDeclarationSyntax>().Where(property => property.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PublicKeyword)
                && !property.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StaticKeyword)).ToArray();
            Require(properties.Length <= 256 && !source.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword), "Settings graph type exceeds the supported source shape.");
            foreach (var property in properties)
            {
                Require(property.AccessorList is not null && property.AccessorList.Accessors.All(accessor => accessor.Body is null && accessor.ExpressionBody is null),
                    "Computed settings graph accessors are unsupported.");
                if (secrets.TryGetValue(name, out var secret) && secret.Contains(property.Identifier.ValueText)) continue;
                instance.Add(property.Identifier.ValueText, Value(property.Initializer?.Value, Type(property), property.SyntaxTree, depth + 1));
            }
        }
        foreach (var entry in initializer?.Expressions ?? [])
        {
            Require(entry is AssignmentExpressionSyntax { Left: IdentifierNameSyntax }, "Settings object overrides require property assignments.");
            var assignment = (AssignmentExpressionSyntax)entry;
            var propertyName = ((IdentifierNameSyntax)assignment.Left).Identifier.ValueText;
            var property = type.GetMembers(propertyName).OfType<IPropertySymbol>().SingleOrDefault();
            Require(property is not null && property.DeclaredAccessibility == Accessibility.Public && !property.IsStatic && instance.ContainsKey(propertyName),
                "Unknown or secret settings object override.");
            instance[propertyName] = Value(assignment.Right, property!.Type, tree, depth + 1);
        }
        return instance;
    }
    /// <summary>Recognizes only BCL list/set collection shapes without running their constructors.</summary>
    private static ITypeSymbol? Collection(ITypeSymbol type) => type is INamedTypeSymbol named
        && named.TypeArguments.Length == 1 && new[] { "System.Collections.Generic.List<T>", "System.Collections.Generic.HashSet<T>",
            "System.Collections.Generic.ICollection<T>", "System.Collections.Generic.IList<T>", "System.Collections.Generic.IReadOnlyList<T>",
            "System.Collections.Generic.ISet<T>" }.Any(name => Native(named, name))
        ? named.TypeArguments[0] : null;
    /// <summary>Recognizes framework symbols from referenced native assemblies, rejecting source name impersonation.</summary>
    private static bool Native(ITypeSymbol type, string name)
    {
        var definition = type.OriginalDefinition;
        var identity = definition.ContainingAssembly?.Identity;
        return definition.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString() == name && definition.DeclaringSyntaxReferences.Length == 0
            && identity is not null && identity.Name is "System.Runtime" or "System.Collections" or "System.Private.CoreLib" or "netstandard"
            && Convert.ToHexStringLower(identity.PublicKeyToken.AsSpan()) is "b03f5f7f11d50a3a" or "7cec85d7bea7798e" or "cc7b13ffcd2ddd51";
    }
    /// <summary>Uses one class identity for nullable and non-nullable references without dropping value nullability.</summary>
    private static string Nominal(ITypeSymbol type) => Unwrap(type).WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString();
    /// <summary>Admits every imported node before cloning, rejecting shape, secret and resource drift.</summary>
    private void ReserveGraph(JsonNode? value, ITypeSymbol type, int depth)
    {
        Require(++nodes <= 4096 && depth <= 16, "Imported settings graph exceeds 4096 nodes or depth16.");
        remainingBytes -= 32;
        Require(remainingBytes >= 0, "Imported settings graph exceeds the 2 MiB retained budget before cloning.");
        if (value is null)
        {
            Require(type.NullableAnnotation == NullableAnnotation.Annotated
                || type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T }, "Imported null graph value is not nullable.");
            return;
        }
        type = Unwrap(type);
        var kind = Kind(type);
        if (value is JsonValue scalar)
        {
            var valid = kind switch { "string" => scalar.TryGetValue<string>(out _), "boolean" => scalar.TryGetValue<bool>(out _),
                "integer" => scalar.TryGetValue<long>(out _), "number" => scalar.TryGetValue<double>(out _), _ => false };
            Require(valid, "Imported graph scalar kind differs from its compiled source type.");
            if (scalar.TryGetValue<string>(out var text))
            {
                Require(text.Length <= 16_384, "Imported graph string exceeds 16 Ki-character limit.");
                remainingBytes -= checked(text.Length * 6);
                Require(remainingBytes >= 0, "Imported settings graph exceeds the 2 MiB retained budget before cloning.");
            }
            return;
        }
        if (value is JsonArray array)
        {
            var element = type is IArrayTypeSymbol nativeArray ? nativeArray.ElementType : Collection(type);
            Require(kind == "array" && element is not null && array.Count <= 256, "Imported settings array shape exceeds its compiled type/256-item limit.");
            foreach (var item in array) ReserveGraph(item, element!, depth + 1);
            return;
        }
        Require(value is JsonObject && kind == "object", "Imported settings graph container differs from its compiled type.");
        var obj = (JsonObject)value;
        Require(obj.Count <= 256, "Imported settings object exceeds 256-field limit.");
        if (type is INamedTypeSymbol dictionary && Native(dictionary, "System.Collections.Generic.Dictionary<TKey, TValue>"))
        {
            Require(dictionary.TypeArguments[0].SpecialType == SpecialType.System_String, "Imported dictionary requires string keys.");
            foreach (var field in obj)
            {
                Require(field.Key.Length is > 0 and <= 4096, "Imported dictionary key exceeds its finite limit.");
                remainingBytes -= checked(field.Key.Length * 6);
                Require(remainingBytes >= 0, "Imported settings graph exceeds the 2 MiB retained budget.");
                ReserveGraph(field.Value, dictionary.TypeArguments[1], depth + 1);
            }
            return;
        }
        var properties = type.GetMembers().OfType<IPropertySymbol>().Where(property => property.DeclaredAccessibility == Accessibility.Public && !property.IsStatic).ToArray();
        secrets.TryGetValue(Nominal(type), out var hidden);
        Require(obj.Select(field => field.Key).ToHashSet(StringComparer.Ordinal).SetEquals(properties.Where(property => hidden?.Contains(property.Name) != true).Select(property => property.Name)),
            "Imported object graph omits properties or carries secret/unknown fields.");
        foreach (var field in obj) ReserveGraph(field.Value, properties.Single(property => property.Name == field.Key).Type, depth + 1);
    }
    /// <summary>Copies only finite compiler constant value kinds into JSON.</summary>
    private static JsonNode? JsonSerializerNode(object? value) => value switch
    {
        null => null, string item => JsonValue.Create(item), bool item => JsonValue.Create(item), int item => JsonValue.Create(item),
        long item => JsonValue.Create(item), double item => JsonValue.Create(item), float item => JsonValue.Create(item), decimal item => JsonValue.Create(item),
        _ => throw new InvalidDataException("Unsupported constant source type.")
    };
    /// <summary>Reads a bounded dependency document with duplicate-key rejection.</summary>
    private static JsonObject ReadMetadata(string path)
    {
        Require(new FileInfo(path).Length <= 2_097_152, "Dependency settings metadata exceeds 2 MiB.");
        return DescriptorContract.Read(File.ReadAllText(path));
    }
    /// <summary>Hashes finite dependency inputs with a streaming native hash.</summary>
    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        Require(stream.Length <= 268_435_456, "Settings dependency exceeds 256 MiB.");
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
    /// <summary>Requires explicit finite dependency identities and scopes.</summary>
    private static string Text(JsonNode? value) => value?.GetValue<string>() is { Length: > 0 and <= 4096 } text
        ? text : throw new InvalidDataException("Settings import requires finite nonempty text.");
    /// <summary>Produces the existing bounded publisher metadata diagnostic on unsupported input.</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
