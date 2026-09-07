namespace Orbyss.Foundation.OpenApiExport;

/// <summary>Describes a platform feature whose package predates consumer feature metadata.</summary>
internal sealed record BuiltInFeatureDefinition(
    string PackageId,
    string[] Dependencies,
    string[] Routes,
    bool ComposeForOpenApi);
