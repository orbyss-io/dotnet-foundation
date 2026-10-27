using Microsoft.OpenApi;

namespace Orbyss.Foundation.Json.AspNetCore;

/// <summary>Owns the numeric JSON kinds and named-string restriction for one serializer path.</summary>
internal readonly record struct JsonNumericSchemaShape(
    JsonSchemaType Types,
    JsonNumericStringKind Strings,
    bool PreservePattern
);
