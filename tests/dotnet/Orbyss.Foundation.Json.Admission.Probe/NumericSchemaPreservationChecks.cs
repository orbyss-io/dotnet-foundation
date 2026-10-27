using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Orbyss.Foundation.Json.Admission.Probe.Contracts;
using Orbyss.Foundation.Json.AspNetCore;

namespace Orbyss.Foundation.Json.Admission.Probe;

/// <summary>Checks that numeric correction preserves independently owned constraints and shared references.</summary>
internal static class NumericSchemaPreservationChecks
{
    /// <summary>Exercises actual profile metadata with precise native schema objects.</summary>
    internal static async Task RunAsync()
    {
        var options = new FoundationJsonOptions();
        var profiles = new JsonProfileCatalog(options.Profiles);
        var definitions = new IJsonContractMetadata[] { StrictNumbers.Response };
        var transformer = new JsonContractSchemaTransformer(profiles, definitions);
        var document = new OpenApiDocument { Components = new OpenApiComponents() };
        var shared = new OpenApiSchema { Type = JsonSchemaType.Integer | JsonSchemaType.String | JsonSchemaType.Null };
        document.Components.Schemas = new Dictionary<string, IOpenApiSchema> { ["SharedScalar"] = shared };
        document.RegisterComponents();
        var reference = new OpenApiSchemaReference(
            "SharedScalar",
            document,
            null
        );
        var alternative = new OpenApiSchema { Type = JsonSchemaType.Integer | JsonSchemaType.Null, Minimum = "0" };
        var alternatives = new List<IOpenApiSchema> { alternative };
        var count = new OpenApiSchema
        {
            Type = JsonSchemaType.Integer | JsonSchemaType.String | JsonSchemaType.Null,
            Description = "Owned count constraints.",
            Pattern = "^[0-9]+$",
            Minimum = "0",
            AnyOf = alternatives,
            OneOf = alternatives
        };
        var root = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema> { ["count"] = count, ["size"] = reference },
            Required = new HashSet<string> { "count", "size" }
        };
        var services = new ServiceCollection();
        using var provider = services.BuildServiceProvider();
        var profile = profiles.Get(JsonProfileKeys.SuccessResponse);
        var info = profile.TypeInfo<StrictNumbers>();
        var context = new OpenApiSchemaTransformerContext
        {
            DocumentName = "v1",
            ParameterDescription = null,
            JsonPropertyInfo = null,
            ApplicationServices = provider,
            JsonTypeInfo = info,
            Document = document
        };

        await transformer.TransformAsync(
            root,
            context,
            CancellationToken.None
        );
        Require(count.Type == (JsonSchemaType.Integer | JsonSchemaType.Null), "strict metadata was not reconciled");
        var constraintsPreserved = count.Pattern == "^[0-9]+$" && count.Minimum == "0" && count.Description == "Owned count constraints."
            && ReferenceEquals(count.AnyOf, alternatives) && ReferenceEquals(count.OneOf, alternatives);
        Require(
constraintsPreserved,
            "owned constraints were erased"
        );
        Require(shared.Type == (JsonSchemaType.Integer | JsonSchemaType.String | JsonSchemaType.Null), "shared reference target was mutated globally");
        var referencePreserved = root.Properties["size"] is OpenApiSchema { AllOf: { Count: 1 } } narrowed
            && ReferenceEquals(narrowed.AllOf[0], reference);
        Require(
referencePreserved,
            "the original shared reference was removed"
        );
        var requirednessPreserved = root.Required.SetEquals(new[] { "count", "size" });
        Require(requirednessPreserved, "numeric correction changed requiredness");

        await VerifyDeclaredPatternAsync(profiles, provider);
    }

    /// <summary>Preserves explicit annotation ownership even for text identical to the known producer artifact.</summary>
    private static async Task VerifyDeclaredPatternAsync(JsonProfileCatalog profiles, IServiceProvider provider)
    {
        var definition = JsonContractMetadata<PatternNumbers>.Response();
        var definitions = new IJsonContractMetadata[] { definition };
        var transformer = new JsonContractSchemaTransformer(profiles, definitions);
        var property = new OpenApiSchema { Type = JsonSchemaType.Integer | JsonSchemaType.String | JsonSchemaType.Null, Pattern = "^-?(?:0|[1-9]\\d*)$" };
        var root = new OpenApiSchema { Properties = new Dictionary<string, IOpenApiSchema> { ["count"] = property } };
        var profile = profiles.Get(JsonProfileKeys.SuccessResponse);
        var context = new OpenApiSchemaTransformerContext
        {
            DocumentName = "v1",
            ParameterDescription = null,
            JsonPropertyInfo = null,
            ApplicationServices = provider,
            JsonTypeInfo = profile.TypeInfo<PatternNumbers>(),
            Document = new OpenApiDocument()
        };

        await transformer.TransformAsync(
            root,
            context,
            CancellationToken.None
        );
        Require(property.Pattern == "^-?(?:0|[1-9]\\d*)$", "an explicitly owned numeric pattern was treated as a producer artifact");
    }

    /// <summary>Uses an independently asserted preservation oracle.</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
