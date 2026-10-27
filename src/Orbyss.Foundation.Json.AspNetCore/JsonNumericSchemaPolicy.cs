using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.Json.Nodes;
using System.ComponentModel.DataAnnotations;
using Microsoft.OpenApi;

namespace Orbyss.Foundation.Json.AspNetCore;

/// <summary>Projects built-in numeric reading or writing policy while preserving independently owned schema constraints.</summary>
internal static class JsonNumericSchemaPolicy
{
    /// <summary>Describes numeric paths without traversing nested object schemas a second time.</summary>
    internal static IReadOnlyDictionary<string, JsonNumericSchemaShape> Describe(JsonTypeInfo info, JsonContractDirection direction)
    {
        var result = new Dictionary<string, JsonNumericSchemaShape>(StringComparer.Ordinal);
        if (info.Kind == JsonTypeInfoKind.Object)
        {
            foreach (var property in info.Properties)
            {
                if (property.CustomConverter is not null || (property.Get is null && property.Set is null))
                    continue;
                var handling = property.NumberHandling ?? info.NumberHandling ?? info.Options.NumberHandling;
                var path = PropertyPath(property.Name);
                var visited = new HashSet<Type>();
                var preservePattern = property.AttributeProvider?.IsDefined(typeof(RegularExpressionAttribute), inherit: true) == true;
                Collect(
                    info.Options,
                    property.PropertyType,
                    handling,
                    direction,
                    path,
                    preservePattern,
                    visited,
                    result
                );
            }
        }
        else
        {
            var handling = info.NumberHandling ?? info.Options.NumberHandling;
            var visited = new HashSet<Type>();
            Collect(
                info.Options,
                info.Type,
                handling,
                direction,
                "$",
                false,
                visited,
                result
            );
        }

        return result;
    }

    /// <summary>Corrects only numeric type assertions; patterns, bounds, references and union constraints are retained.</summary>
    internal static void Apply(OpenApiSchema schema, JsonContractSchemaPlan plan)
    {
        var info = plan.TypeInfo;
        if (info.Kind == JsonTypeInfoKind.Object)
        {
            foreach (var property in info.Properties)
            {
                var path = PropertyPath(property.Name);
                if (property.CustomConverter is not null || !HasPath(plan.NumericTypes, path))
                    continue;
                if (schema.Properties is null || !schema.Properties.TryGetValue(property.Name, out var declared))
                    throw new InvalidOperationException("OpenAPI is missing a registered JSON numeric property.");

                schema.Properties[property.Name] = ApplyPath(
                    declared,
                    info.Options,
                    property.PropertyType,
                    path,
                    plan.NumericTypes
                );
            }
        }
        else
        {
            _ = ApplyPath(
                schema,
                info.Options,
                info.Type,
                "$",
                plan.NumericTypes
            );
        }
    }

    /// <summary>Finds paths belonging to one actual serialized property, including numeric collection values.</summary>
    private static bool HasPath(IReadOnlyDictionary<string, JsonNumericSchemaShape> paths, string name)
    {
        foreach (var path in paths.Keys)
        {
            if (path == name || path.StartsWith(name + "/", StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>Uses unambiguous JSON Pointer property segments even when wire names contain slashes or tildes.</summary>
    private static string PropertyPath(string name)
    {
        var escaped = name
            .Replace(
                "~",
                "~0",
                StringComparison.Ordinal
            )
            .Replace(
                "/",
                "~1",
                StringComparison.Ordinal
            );
        return "/properties/" + escaped;
    }

    /// <summary>Collects standard numeric policy finitely; opaque converters retain their native schema owner.</summary>
    private static void Collect(
        JsonSerializerOptions options,
        Type type,
        JsonNumberHandling handling,
        JsonContractDirection direction,
        string path,
        bool preservePattern,
        ISet<Type> visited,
        IDictionary<string, JsonNumericSchemaShape> result
    )
    {
        if (!visited.Add(type))
            return;
        var actual = Nullable.GetUnderlyingType(type) ?? type;
        var info = options.GetTypeInfo(type);
        var numeric = NumericKind(actual);
        if (numeric is { } numericKind)
        {
            var underlying = options.GetTypeInfo(actual);
            if (info.Converter.GetType().Assembly != typeof(JsonSerializer).Assembly
                || underlying.Converter.GetType().Assembly != typeof(JsonSerializer).Assembly)
                return;

            var floating = actual == typeof(float) || actual == typeof(double) || actual == typeof(Half);
            var readStrings = (handling & JsonNumberHandling.AllowReadingFromString) != 0;
            var writeStrings = (handling & JsonNumberHandling.WriteAsString) != 0;
            var named = floating && (handling & JsonNumberHandling.AllowNamedFloatingPointLiterals) != 0;
            var strings = JsonNumericStringKind.None;
            var kinds = numericKind;
            if (direction == JsonContractDirection.Request)
            {
                if (readStrings)
                    strings = floating ? JsonNumericStringKind.NumericAndNamed : JsonNumericStringKind.Numeric;
                else if (named)
                    strings = JsonNumericStringKind.Named;
            }
            else if (writeStrings)
            {
                kinds = JsonSchemaType.String;
                strings = named ? JsonNumericStringKind.NumericAndNamed : JsonNumericStringKind.Numeric;
            }
            else if (named)
            {
                strings = JsonNumericStringKind.Named;
            }

            if (strings != JsonNumericStringKind.None)
                kinds |= JsonSchemaType.String;
            if (actual != type)
                kinds |= JsonSchemaType.Null;
            var shape = new JsonNumericSchemaShape(
                kinds,
                strings,
                preservePattern
            );
            result.Add(path, shape);
            return;
        }

        if (info.ElementType is { } element && info.Kind is JsonTypeInfoKind.Enumerable or JsonTypeInfoKind.Dictionary)
        {
            var suffix = info.Kind == JsonTypeInfoKind.Dictionary ? "/additionalProperties" : "/items";
            Collect(
                options,
                element,
                handling,
                direction,
                path + suffix,
                preservePattern,
                visited,
                result
            );
        }
    }

    /// <summary>Selects standard numeric JSON primitives without treating arbitrary value types as numbers.</summary>
    private static JsonSchemaType? NumericKind(Type type)
    {
        if (type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort)
            || type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong)
            || type == typeof(Int128) || type == typeof(UInt128))
            return JsonSchemaType.Integer;
        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal) || type == typeof(Half))
            return JsonSchemaType.Number;
        return null;
    }

    /// <summary>Preserves explicit reference constraints when narrowing a referenced numeric union.</summary>
    private static IOpenApiSchema ApplyPath(
        IOpenApiSchema schema,
        JsonSerializerOptions options,
        Type type,
        string path,
        IReadOnlyDictionary<string, JsonNumericSchemaShape> kinds
    )
    {
        if (kinds.TryGetValue(path, out var expected))
        {
            if (schema is OpenApiSchema direct)
            {
                direct.Type = expected.Types;
                if (!expected.PreservePattern)
                    RemoveProducerPattern(direct);
                RestrictNamedStrings(direct, expected);
                return direct;
            }

            if (schema.Type is not { } original || (original & expected.Types) != expected.Types)
                throw new InvalidOperationException("A referenced numeric schema cannot represent the registered JSON contract without changing its constraints.");
            var references = new List<IOpenApiSchema> { schema };
            var constrained = new OpenApiSchema { Type = expected.Types, AllOf = references };
            RestrictNamedStrings(constrained, expected);
            return constrained;
        }

        var info = options.GetTypeInfo(type);
        if (info.ElementType is not { } element || schema is not OpenApiSchema container)
            throw new InvalidOperationException("OpenAPI cannot reconcile a registered numeric collection shape.");
        if (info.Kind == JsonTypeInfoKind.Enumerable && container.Items is { } item)
            container.Items = ApplyPath(
                item,
                options,
                element,
                path + "/items",
                kinds
            );
        else if (info.Kind == JsonTypeInfoKind.Dictionary && container.AdditionalProperties is { } value)
            container.AdditionalProperties = ApplyPath(
                value,
                options,
                element,
                path + "/additionalProperties",
                kinds
            );
        else
            throw new InvalidOperationException("OpenAPI is missing registered numeric collection metadata.");

        return container;
    }

    /// <summary>Removes only known native numeric-string regexes that reject valid serializer input such as leading plus/zeros.</summary>
    private static void RemoveProducerPattern(OpenApiSchema schema)
    {
        if (schema.Pattern is not { } pattern)
            return;
        var normalized = pattern.Replace(
            "\\\\",
            "\\",
            StringComparison.Ordinal
        );
        if (normalized is "^-?(?:0|[1-9]\\d*)$" or "^-?(?:0|[1-9]\\d*)(?:\\.\\d+)?$"
            or "^-?(?:0|[1-9]\\d*)(?:\\.\\d+)?(?:[eE][+-]?\\d+)?$")
            schema.Pattern = null;
    }

    /// <summary>Keeps named-only floating strings finite while leaving existing unions and constraints intact.</summary>
    private static void RestrictNamedStrings(OpenApiSchema schema, JsonNumericSchemaShape shape)
    {
        if (shape.Strings != JsonNumericStringKind.Named)
            return;
        var numeric = new OpenApiSchema { Type = shape.Types & ~JsonSchemaType.String };
        var names = new List<JsonNode> { JsonValue.Create("NaN")!, JsonValue.Create("Infinity")!, JsonValue.Create("-Infinity")! };
        var named = new OpenApiSchema { Type = JsonSchemaType.String, Enum = names };
        var alternatives = new List<IOpenApiSchema> { numeric, named };
        var restriction = new OpenApiSchema { AnyOf = alternatives };
        schema.AllOf ??= new List<IOpenApiSchema>();
        schema.AllOf.Add(restriction);
    }
}
