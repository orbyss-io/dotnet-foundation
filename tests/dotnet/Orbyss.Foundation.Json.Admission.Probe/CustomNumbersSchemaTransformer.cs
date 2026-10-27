using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Orbyss.Foundation.Json.Admission.Probe.Contracts;

namespace Orbyss.Foundation.Json.Admission.Probe;

/// <summary>Uses the existing native extension point for the custom converter's application-owned schema.</summary>
internal sealed class CustomNumbersSchemaTransformer : IOpenApiSchemaTransformer
{
    /// <inheritdoc />
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.JsonTypeInfo.Type == typeof(CustomNumbers) && schema.Properties?["number"] is OpenApiSchema number)
        {
            number.Type = JsonSchemaType.String | JsonSchemaType.Null;
            number.Format = null;
            number.Pattern = "^id:[0-9]+$";
        }

        return Task.CompletedTask;
    }
}
